#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json, struct, zipfile, zlib
from pathlib import Path

FIXED_TIMESTAMP=(1980,1,1,0,0,0)

def canonical(obj):
    return (json.dumps(obj,ensure_ascii=False,sort_keys=True,separators=(",",":"))+"\n").encode("utf-8")

def chunk(kind,data):
    return struct.pack(">I",len(data))+kind+data+struct.pack(">I",zlib.crc32(kind+data)&0xffffffff)

def make_png(w,h,bg,rects):
    pix=bytearray(bytes(bg)*w*h)
    for x,y,rw,rh,c in rects:
        row=bytes(c)*rw
        for yy in range(y,y+rh):
            off=(yy*w+x)*3
            pix[off:off+len(row)]=row
    raw=b"".join(b"\x00"+bytes(pix[y*w*3:(y+1)*w*3]) for y in range(h))
    return b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",w,h,8,2,0,0,0))+chunk(b"IDAT",zlib.compress(raw,9))+chunk(b"IEND",b"")

def put(z,name,data):
    info=zipfile.ZipInfo(name,date_time=FIXED_TIMESTAMP); info.compress_type=zipfile.ZIP_DEFLATED; info.create_system=3; info.external_attr=0o100644<<16
    z.writestr(info,data,compress_type=zipfile.ZIP_DEFLATED,compresslevel=9)

def build_type2(path):
    manifest={"formatVersion":"1.0","packageId":"6121267f-f4cb-435e-a74e-f180624a1374","author":"ScratchGame Test","minimumAppVersion":"0.5.4","ticketFile":"ticket.json"}
    zones=[]
    for i,x in enumerate((240,450,660),1):
        zones.append({"id":f"winning-{i:02d}","x":x,"y":175,"width":180,"height":95,"shape":"roundedRectangle","cornerRadius":14})
    n=1
    for y in (345,475,605):
        for x in (120,330,540,750):
            zones.append({"id":f"play-{n:02d}","x":x,"y":y,"width":180,"height":95,"shape":"roundedRectangle","cornerRadius":14}); n+=1
    ticket={"name":"中獎號碼（測試）","price":100,"canvas":1,"priceDisplay":1,"priceDisplayArea":{"x":850,"y":35,"width":180,"height":72},"gameType":"2","issueSize":8,"ticketsPerBook":8,"art":{"ticket":{"source":"package","ref":"assets/ticket.png"}},"serialDisplayArea":{"x":365,"y":810,"width":350,"height":50},"scratch":{"foil":{"source":"package","ref":"assets/foil.png"},"zones":zones},"game":{"winningNumberCount":3,"playNumberCount":12,"numberMin":1,"numberMax":30,"payoutSource":"play","displayPrizeAmounts":[100,200,500,1000],"allowPrizeAmountRepeat":True},"prizes":[{"amount":100,"count":1},{"amount":300,"count":1},{"amount":700,"count":1},{"amount":2000,"count":1}]}
    rects=[(65,75,950,690,(28,66,108)),(65,75,950,72,(22,52,92)),(840,28,205,95,(22,52,92)),(350,795,380,75,(14,33,62)),(190,145,700,155,(47,91,137)),(85,315,875,420,(39,78,121))]
    for x in (240,450,660): rects.append((x-8,167,196,111,(226,236,245)))
    for y in (345,475,605):
        for x in (120,330,540,750): rects.append((x-8,y-8,196,111,(234,240,247)))
    ticket_png=make_png(1080,882,(18,39,72),rects); foil_png=make_png(32,32,(194,199,205),[])
    with zipfile.ZipFile(path,"w") as z:
        put(z,"manifest.json",canonical(manifest)); put(z,"ticket.json",canonical(ticket)); put(z,"assets/ticket.png",ticket_png); put(z,"assets/foil.png",foil_png)

def build_type3(path):
    manifest={"formatVersion":"1.0","packageId":"f2b40a25-2243-4a14-b680-78c377c55403","author":"ScratchGame Test","minimumAppVersion":"0.5.6","ticketFile":"ticket.json"}
    zones=[]; idx=1
    for y in (180,330,480):
        for x in (150,390,630):
            zones.append({"id":f"amount-{idx:02d}","x":x,"y":y,"width":210,"height":110,"shape":"roundedRectangle","cornerRadius":14}); idx+=1
    ticket={"name":"三個相同（測試）","price":100,"canvas":1,"priceDisplay":1,"priceDisplayArea":{"x":850,"y":40,"width":180,"height":72},"gameType":"3","issueSize":8,"ticketsPerBook":8,"art":{"ticket":{"source":"package","ref":"assets/ticket.png"}},"serialDisplayArea":{"x":365,"y":810,"width":350,"height":50},"scratch":{"foil":{"source":"package","ref":"assets/foil.png"},"zones":zones},"game":{"zoneCount":9,"useCustomDecoyAmounts":True,"decoyAmounts":[50,200,750,2000,5000,20000],"nearMissPairProbability":75,"nearMissPairCount":1},"prizes":[{"amount":100,"count":1},{"amount":500,"count":1},{"amount":1000,"count":1}]}
    rects=[(70,80,940,680,(92,45,29)),(70,80,940,65,(112,52,34)),(840,30,200,95,(112,52,34)),(350,795,380,75,(73,31,22))]
    for y in (180,330,480):
        for x in (150,390,630): rects.append((x-8,y-8,226,126,(238,218,180)))
    ticket_png=make_png(1080,882,(49,20,14),rects); foil_png=make_png(32,32,(198,198,192),[])
    with zipfile.ZipFile(path,"w") as z:
        put(z,"manifest.json",canonical(manifest)); put(z,"ticket.json",canonical(ticket)); put(z,"assets/ticket.png",ticket_png); put(z,"assets/foil.png",foil_png)

def build_type4(path):
    manifest={"formatVersion":"1.0","packageId":"95e7ee6d-5234-4a3b-a6c0-b1299b942436","author":"ScratchGame Test","minimumAppVersion":"0.5.7","ticketFile":"ticket.json"}
    zones=[]; idx=1
    for y in (190,325,460):
        for x in (105,325,545,765):
            zones.append({"id":f"symbol-{idx:02d}","x":x,"y":y,"width":175,"height":100,"shape":"roundedRectangle","cornerRadius":14}); idx+=1
    ticket={"name":"符號計數（測試）","price":100,"canvas":1,"priceDisplay":1,"priceDisplayArea":{"x":850,"y":40,"width":180,"height":72},"gameType":"4","issueSize":8,"ticketsPerBook":8,"art":{"ticket":{"source":"package","ref":"assets/ticket.png"}},"serialDisplayArea":{"x":365,"y":810,"width":350,"height":50},"scratch":{"foil":{"source":"package","ref":"assets/foil.png"},"zones":zones},"game":{"mode":"multiSymbolFixedCount","zoneCount":12,"matchCount":3,"symbolPrizes":[{"symbol":"★","amount":100},{"symbol":"○","amount":300},{"symbol":"◆","amount":700}],"allowMultipleWins":True,"useCustomDecoySymbols":True,"decoySymbols":["●","▲","■","♥"]},"prizes":[{"amount":100,"count":1},{"amount":300,"count":1},{"amount":400,"count":1},{"amount":700,"count":1},{"amount":800,"count":1},{"amount":1000,"count":1},{"amount":1100,"count":1}]}
    rects=[(70,80,940,690,(37,73,58)),(70,80,940,70,(50,94,74)),(840,30,200,95,(50,94,74)),(350,795,380,75,(25,50,40))]
    for y in (190,325,460):
        for x in (105,325,545,765): rects.append((x-8,y-8,191,116,(232,238,220)))
    ticket_png=make_png(1080,882,(20,45,35),rects); foil_png=make_png(32,32,(196,201,193),[])
    with zipfile.ZipFile(path,"w") as z:
        put(z,"manifest.json",canonical(manifest)); put(z,"ticket.json",canonical(ticket)); put(z,"assets/ticket.png",ticket_png); put(z,"assets/foil.png",foil_png)

def digest(path):
    data=path.read_bytes(); return len(data),hashlib.sha256(data).hexdigest()

def main():
    p=argparse.ArgumentParser(); p.add_argument('--output-dir',required=True,type=Path); a=p.parse_args(); a.output_dir.mkdir(parents=True,exist_ok=True)
    outputs=[('GameType2-Test.scratchpack',build_type2),('GameType3-Test.scratchpack',build_type3),('GameType4-Test.scratchpack',build_type4)]
    for name,fn in outputs:
        path=a.output_dir/name; fn(path); size,sha=digest(path); print(f'{name}\t{size}\t{sha}')
if __name__=='__main__': main()
