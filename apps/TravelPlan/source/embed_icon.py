#!/usr/bin/env python3
"""Embed ICO + Windows manifest into a PE32+ executable as a .rsrc section.
Build helper only; no third-party dependencies.
"""
from __future__ import annotations
import struct, sys
from pathlib import Path

RT_ICON=3; RT_GROUP_ICON=14; RT_MANIFEST=24; LANG=0x0409
SCN_CNT_INITIALIZED_DATA=0x00000040; SCN_MEM_READ=0x40000000

def align(v,a): return (v+a-1)//a*a

def parse_ico(path:Path):
    b=path.read_bytes(); rsv,typ,count=struct.unpack_from('<HHH',b,0)
    if rsv!=0 or typ!=1 or count<1: raise ValueError('invalid ico')
    entries=[]
    for i in range(count):
        off=6+i*16
        w,h,cc,res,planes,bpp,size,img_off=struct.unpack_from('<BBBBHHII',b,off)
        data=b[img_off:img_off+size]
        if len(data)!=size: raise ValueError('truncated ico')
        entries.append((w,h,cc,res,planes,bpp,size,data))
    return entries

def group_icon(entries):
    out=bytearray(struct.pack('<HHH',0,1,len(entries)))
    for idx,(w,h,cc,res,planes,bpp,size,data) in enumerate(entries,1):
        out += struct.pack('<BBBBHHIH',w,h,cc,res,planes,bpp,size,idx)
    return bytes(out)

def build_rsrc(section_rva:int, ico_entries, manifest:bytes):
    resources=[]
    for i,e in enumerate(ico_entries,1): resources.append((RT_ICON,i,LANG,e[-1]))
    if ico_entries:
        resources.append((RT_GROUP_ICON,1,LANG,group_icon(ico_entries)))
    resources.append((RT_MANIFEST,1,LANG,manifest))
    # type -> id -> lang -> data
    tree={}
    for typ,rid,lang,data in resources: tree.setdefault(typ,{}).setdefault(rid,{})[lang]=data
    types=sorted(tree)
    # allocate directory tables first
    off=0
    root_off=off; off+=16+8*len(types)
    type_dir={}
    id_dir={}
    for typ in types:
        type_dir[typ]=off; off+=16+8*len(tree[typ])
    for typ in types:
        for rid in sorted(tree[typ]):
            id_dir[(typ,rid)]=off; off+=16+8*len(tree[typ][rid])
    # data entries
    data_entry={}
    for typ in types:
        for rid in sorted(tree[typ]):
            for lang in sorted(tree[typ][rid]):
                data_entry[(typ,rid,lang)]=off; off+=16
    off=align(off,4)
    raw_off={}
    for typ in types:
        for rid in sorted(tree[typ]):
            for lang in sorted(tree[typ][rid]):
                raw_off[(typ,rid,lang)]=off; off+=len(tree[typ][rid][lang]); off=align(off,4)
    buf=bytearray(off)
    def put_dir(pos,entries):
        struct.pack_into('<IIHHHH',buf,pos,0,0,0,0,0,len(entries))
        q=pos+16
        for name,target,isdir in entries:
            struct.pack_into('<II',buf,q,name,(0x80000000 if isdir else 0)|target);q+=8
    put_dir(root_off,[(typ,type_dir[typ],True) for typ in types])
    for typ in types:
        put_dir(type_dir[typ],[(rid,id_dir[(typ,rid)],True) for rid in sorted(tree[typ])])
    for typ in types:
        for rid in sorted(tree[typ]):
            put_dir(id_dir[(typ,rid)],[(lang,data_entry[(typ,rid,lang)],False) for lang in sorted(tree[typ][rid])])
    for typ in types:
        for rid in sorted(tree[typ]):
            for lang,data in tree[typ][rid].items():
                ro=raw_off[(typ,rid,lang)]
                de=data_entry[(typ,rid,lang)]
                struct.pack_into('<IIII',buf,de,section_rva+ro,len(data),0,0)
                buf[ro:ro+len(data)]=data
    return bytes(buf)

def embed(exe:Path, ico:Path | None = None):
    b=bytearray(exe.read_bytes())
    if b[:2]!=b'MZ': raise ValueError('not MZ')
    pe=struct.unpack_from('<I',b,0x3c)[0]
    if b[pe:pe+4]!=b'PE\0\0': raise ValueError('not PE')
    coff=pe+4
    machine,nsec,tstamp,ptrsym,nsym,opt_size,chars=struct.unpack_from('<HHIIIHH',b,coff)
    opt=coff+20
    magic=struct.unpack_from('<H',b,opt)[0]
    if magic!=0x20b: raise ValueError('only PE32+ supported')
    sec_align=struct.unpack_from('<I',b,opt+32)[0]
    file_align=struct.unpack_from('<I',b,opt+36)[0]
    size_headers=struct.unpack_from('<I',b,opt+60)[0]
    dd=opt+112
    sec_table=opt+opt_size
    secs=[]
    for i in range(nsec):
        sh=sec_table+i*40
        name=bytes(b[sh:sh+8]).split(b'\0')[0]
        vsize,va,rawsize,rawptr=struct.unpack_from('<IIII',b,sh+8)
        secs.append((name,vsize,va,rawsize,rawptr,sh))
    first_raw=min(s[4] for s in secs if s[4])
    new_sh=sec_table+nsec*40
    if new_sh+40>first_raw: raise ValueError('no room for section header')
    last=max(secs,key=lambda x:x[2])
    new_va=align(last[2]+max(last[1],last[3]),sec_align)
    manifest=b'''<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
 <assemblyIdentity version="1.0.0.0" processorArchitecture="amd64" name="TravelPlan" type="win32"/>
 <description>TravelPlan desktop application</description>
 <dependency><dependentAssembly><assemblyIdentity type="win32" name="Microsoft.Windows.Common-Controls" version="6.0.0.0" processorArchitecture="*" publicKeyToken="6595b64144ccf1df" language="*"/></dependentAssembly></dependency>
 <application xmlns="urn:schemas-microsoft-com:asm.v3"><windowsSettings><dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware></windowsSettings></application>
</assembly>'''
    if any(s[0] == b'.rsrc' for s in secs):
        raise ValueError('executable already contains resources; rebuild before embedding')
    rsrc=build_rsrc(new_va,parse_ico(ico) if ico else [],manifest)
    rawptr=align(len(b),file_align); rawsize=align(len(rsrc),file_align)
    if len(b)<rawptr:b+=b'\0'*(rawptr-len(b))
    b+=rsrc+b'\0'*(rawsize-len(rsrc))
    # new section header
    name=b'.rsrc\0\0\0'
    b[new_sh:new_sh+8]=name
    struct.pack_into('<IIIIIIHHI',b,new_sh+8,len(rsrc),new_va,rawsize,rawptr,0,0,0,0,SCN_CNT_INITIALIZED_DATA|SCN_MEM_READ)
    struct.pack_into('<H',b,coff+2,nsec+1)
    # initialized data size
    init_size=struct.unpack_from('<I',b,opt+8)[0];struct.pack_into('<I',b,opt+8,init_size+rawsize)
    # resource directory entry
    struct.pack_into('<II',b,dd+2*8,new_va,len(rsrc))
    # size of image
    struct.pack_into('<I',b,opt+56,align(new_va+len(rsrc),sec_align))
    exe.write_bytes(b)

def inspect(exe:Path):
    b=exe.read_bytes();pe=struct.unpack_from('<I',b,0x3c)[0];coff=pe+4;nsec=struct.unpack_from('<H',b,coff+2)[0];opt_size=struct.unpack_from('<H',b,coff+16)[0];opt=coff+20;dd=opt+112;rr=struct.unpack_from('<II',b,dd+16);sec=opt+opt_size
    names=[]
    for i in range(nsec): names.append(bytes(b[sec+i*40:sec+i*40+8]).split(b'\0')[0].decode(errors='replace'))
    print('sections=',names,'resource=',rr,'size=',len(b))

if __name__=='__main__':
    if len(sys.argv)<2: raise SystemExit('usage: embed_icon.py exe [ico]')
    embed(Path(sys.argv[1]),Path(sys.argv[2]) if len(sys.argv)>2 else None);inspect(Path(sys.argv[1]))
