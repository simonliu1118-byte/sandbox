'use strict';
const $ = id => document.getElementById(id);
const today = new Date();
const state = {cards:[],days:{}, pending:false};
const daysInMonth=()=>new Date(+$('year').value, +$('month').value, 0).getDate();
const pad=n=>String(n).padStart(2,'0');
const setStatus=(message,cls='')=>{ $('progress').textContent=message; $('progress').className='status '+cls; };
const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function parsePunches(str){
 if(!String(str||'').trim())return [];
 const parts=String(str).trim().split(/[,，、;；\s]+/).filter(Boolean);
 return parts.map(p=>{
   let s=p.replace(/[：﹕]/g,':');
   if(/^\d{3,4}$/.test(s))s=s.slice(0,-2)+':'+s.slice(-2);
   const m=s.match(/^(\d{1,2}):(\d{2})$/);
   if(!m||+m[1]>23||+m[2]>59)throw new Error(`不合法時間：${p}`);
   return (+m[1])*60+(+m[2]);
 }).sort((a,b)=>a-b);
}
function timeStr(n){return pad(Math.floor(n/60))+':'+pad(n%60)};
function calculate(str){
 let x;try{x=parsePunches(str)}catch(e){return {status:e.message,type:'bad',punches:[],actual:null,rounded:null,hours:null,start:'',end:''};}
 const base={punches:x,start:x.length?timeStr(x[0]):'',end:x.length?timeStr(x[x.length-1]):'',actual:null,rounded:null,hours:null};
 if(!x.length)return {...base,status:'未打卡',type:'empty'};
 if(x.length!==2&&x.length!==4)return {...base,status:`${x.length} 筆，請補齊配對`,type:'bad'};
 let actual=0;for(let i=0;i<x.length;i+=2)actual+=x[i+1]-x[i];
 if(actual<0)return {...base,status:'時間順序異常',type:'bad'};
 const rounded=Math.floor(actual/15)*15;
 return {...base,actual,rounded,hours:rounded/60,status:'正常',type:'ok'};
}
function renderRows(){const n=daysInMonth();for(let i=n+1;i<=31;i++)delete state.days[i];let html='';for(let d=1;d<=n;d++){
 const r=state.days[d]||{text:'',note:'',checked:false};const a=calculate(r.text);const isReview=!!r.note&&!r.checked;
 const status=a.type==='ok'?(isReview?'⚠ 辨識待核對':'正常'):a.status;
 const sty=a.type==='bad'?'badtext':isReview?'warntext':a.type==='ok'?'oktext':'';
 html+=`<tr><td>${pad(+$('month').value)}/${pad(d)}</td><td><input type="text" data-day="${d}" class="punch" value="${esc(r.text)}" placeholder="例如 13:45,14:30"></td><td>${a.start}</td><td>${a.end}</td><td>${a.actual??''}</td><td><b>${a.hours===null?'':a.hours.toFixed(2)}</b></td><td class="${sty}" title="${esc(r.note||'')}">${esc(status)}</td><td><input type="checkbox" class="check" data-day="${d}" ${r.checked?'checked':''}></td></tr>`;
 } $('tbody').innerHTML=html; updateTotal();}
function updateTotal(){let sum=0,warnings=0,pending=0;for(let d=1;d<=daysInMonth();d++){
 const r=state.days[d]||{text:'',checked:false,note:''};const c=calculate(r.text);if(c.hours!==null)sum+=c.hours;
 if(c.type==='bad')warnings++;if(r.note&&!r.checked)pending++;
 }$('sum').textContent=sum.toFixed(2);$('warning-summary').textContent=`${warnings} 天打卡待補正；${pending} 天 AI/OCR 資料待人工核對`;}
$('tbody').addEventListener('change',e=>{if(e.target.classList.contains('check')){const d=+e.target.dataset.day;state.days[d]||={text:'',note:'',checked:false};state.days[d].checked=e.target.checked;renderRows();}});
$('tbody').addEventListener('input',e=>{if(e.target.classList.contains('punch')){const d=+e.target.dataset.day;state.days[d]||={text:'',note:'',checked:false};state.days[d].text=e.target.value;
 // Avoid rerender while typing (focus would be lost); refresh only computed columns.
 const row=e.target.closest('tr');const tds=row.querySelectorAll('td');const a=calculate(e.target.value),r=state.days[d],review=!!r.note&&!r.checked;
 tds[2].textContent=a.start;tds[3].textContent=a.end;tds[4].textContent=a.actual??'';tds[5].textContent=a.hours===null?'':a.hours.toFixed(2);
 tds[6].textContent=a.type==='ok'?(review?'⚠ 辨識待核對':'正常'):a.status;
 tds[6].className=a.type==='bad'?'badtext':review?'warntext':a.type==='ok'?'oktext':'';
 updateTotal();}});
$('month').addEventListener('change',renderRows);$('year').addEventListener('change',renderRows);
$('clear').addEventListener('click',()=>{if(confirm('確定清除目前照片和所有已輸入的打卡時間嗎？')){state.cards=[];state.days={};$('files').value='';if(typeof clearAIResults==='function')clearAIResults();renderCards();renderRows();setStatus('已清空。');}});
for(let m=1;m<=12;m++)$('month').add(new Option(`${m} 月`,m));$('month').value=today.getMonth()+1;$('year').value=today.getFullYear();
const configs={front:{left:169,middle:230,right:291,top:424,bottom:893,rows:15,base:1},back:{left:210,middle:275,right:337,top:425,bottom:928,rows:16,base:16}};
$('files').addEventListener('change',async e=>{const picked=Array.from(e.target.files);for(const f of picked){const url=URL.createObjectURL(f);try{const image=await loadImage(url);const side=state.cards.length===0?'front':'back';const k=configs[side];state.cards.push({f,url,image,side,coords:{...k}});}catch(err){alert(f.name+' 讀取失敗：'+err.message);URL.revokeObjectURL(url);}}renderCards();});
function loadImage(src){return new Promise((res,rej)=>{const i=new Image();i.onload=()=>res(i);i.onerror=()=>rej(new Error('圖片格式錯誤'));i.src=src;});}
function renderCards(){let html='';state.cards.forEach((card,i)=>{const c=card.coords;
 html+=`<div class="cardrow"><img src="${card.url}" class="thumb" data-image="${i}" alt="打卡卡照片"><div class="cardcontent"><p><b>照片 ${i+1}：</b>${esc(card.f.name)} <span class="hint">(${card.image.naturalWidth} × ${card.image.naturalHeight})</span></p><div class="flex"><label>卡片面<select data-card="${i}" class="side"><option value="front" ${card.side==='front'?'selected':''}>正面 1～15 日</option><option value="back" ${card.side==='back'?'selected':''}>背面 16～31 日</option></select></label><button class="secondary crop-adjust" data-card="${i}">調整裁切位置</button><button class="secondary remove-card" data-card="${i}">移除照片</button></div><div class="ai-actions"><button type="button" class="secondary ai-preview" data-card="${i}">預覽 AI 上傳範圍</button><button type="button" class="secondary ai-scan" data-card="${i}">AI 辨識此張（需同意）</button></div>
 <details id="adjust-${i}"><summary>裁切格線座標（以範例 750×1000 照片為基準，會按圖片大小等比縮放）</summary><div class="flex" style="margin-top:10px">${[['left','第一格左'],['middle','兩格交界'],['right','第二格右'],['top','首行上緣'],['bottom','最後行下緣']].map(([key,label])=>`<label>${label}<input type="number" class="coord" data-card="${i}" data-key="${key}" value="${c[key]}"></label>`).join('')}</div></details></div></div>`;});$('cards').innerHTML=html;$('ocr').disabled=state.cards.length===0;}
$('cards').addEventListener('click',e=>{const image=e.target.closest('[data-image]');if(image){$('modalimg').src=state.cards[+image.dataset.image].url;$('modal').style.display='flex';}
 const crop=e.target.closest('.crop-adjust');if(crop){const d=$('adjust-'+crop.dataset.card);d.open=!d.open;}
 const rm=e.target.closest('.remove-card');if(rm){const idx=+rm.dataset.card;URL.revokeObjectURL(state.cards[idx].url);state.cards.splice(idx,1);renderCards();}});
$('cards').addEventListener('change',e=>{const idx=+e.target.dataset.card;if(!Number.isInteger(idx)||!state.cards[idx])return;if(e.target.classList.contains('side')){const side=e.target.value;state.cards[idx].side=side;state.cards[idx].coords={...configs[side]};renderCards();}
 if(e.target.classList.contains('coord'))state.cards[idx].coords[e.target.dataset.key]=+e.target.value;});
$('preview-toggle').addEventListener('change',e=>{$('ocr-preview').style.display=e.target.checked?'block':'none'});
function cropComposite(card){
 const {image,coords:c}=card,W=760,rowH=102,rows=c.rows;const canvas=document.createElement('canvas');canvas.width=W;canvas.height=rowH*rows;
 const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.fillStyle='white';ctx.fillRect(0,0,W,canvas.height);
 const sx=image.naturalWidth/750,sy=image.naturalHeight/1000;
 for(let r=0;r<rows;r++){const top=(c.top+(c.bottom-c.top)*r/rows+3)*sy,h=((c.bottom-c.top)/rows-6)*sy;
  for(let side=0;side<2;side++){
   const left=(side===0?c.left:c.middle)+3,right=(side===0?c.middle:c.right)-3;
   const x=side===0?18:399,y=r*rowH+9,w=335,dh=81;
   ctx.drawImage(image,left*sx,top,Math.max(1,(right-left)*sx),Math.max(1,h),x,y,w,dh);
   const dat=ctx.getImageData(x,y,w,dh);let a=dat.data;
   // Suppress pale paper; retain dark ink and dot-matrix print. Leave OCR black on white.
   for(let i=0;i<a.length;i+=4){const g=.299*a[i]+.587*a[i+1]+.114*a[i+2];const v=g>178?255:g<115?0:Math.round((g-115)*255/63);a[i]=a[i+1]=a[i+2]=v;}
   ctx.putImageData(dat,x,y);
  }
 }
 return canvas;
}
function extractTimes(words,rowCount){
 const blocks={};for(let r=0;r<rowCount;r++)blocks[r]=[[],[]];
 for(const w of words){const row=Math.floor((+w.y+Math.max(+w.h,0)/2)/102);if(row<0||row>=rowCount)continue;const col=(+w.x+Math.max(+w.w,0)/2)>=380?1:0;blocks[row][col].push(w);}
 const found=[];
 for(let r=0;r<rowCount;r++){let ts=[];
  for(const items of blocks[r]){
    if(!items.length)continue;items.sort((a,b)=>a.x-b.x);
    const raw=items.map(x=>x.text).join('').replace(/[oO]/g,'0').replace(/[Il|]/g,'1').replace(/[sS]/g,'5');
    const hits=raw.match(/(?:[01]?\d|2[0-3])[:：.](?:[0-5]\d)|\b\d{3,4}\b/g)||[];
    for(const hit of hits){let t=hit.replace(/[：.]/g,':');if(!t.includes(':'))t=t.slice(0,-2)+':'+t.slice(-2);
       try{const mins=parsePunches(t);if(mins.length===1)ts.push(timeStr(mins[0]));}catch(_){}}
  }
  if(ts.length)found.push({row:r,punches:ts});
 }
 return found;
}
$('ocr').addEventListener('click',async()=>{
 if(state.pending)return;state.pending=true;$('ocr').disabled=true;
 let scans=0,fail=0;
 for(let i=0;i<state.cards.length;i++){
 const card=state.cards[i];setStatus(`正在辨識第 ${i+1}/${state.cards.length} 張：${card.f.name}，請稍候…`);
 try{
  const sheet=cropComposite(card);const prev=$('ocr-preview');prev.width=sheet.width;prev.height=sheet.height;prev.getContext('2d').drawImage(sheet,0,0);
  const base64=sheet.toDataURL('image/png').split(',')[1];
  const response=await fetch('/api/ocr',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({image:base64})});
  const json=await response.json();if(!response.ok||!json.ok)throw new Error(json.error||'未知辨識錯誤');
  const records=extractTimes(json.words||[],card.coords.rows);scans+=records.length;
  for(const rec of records){const day=card.coords.base+rec.row;if(day>daysInMonth())continue;
    const txt=rec.punches.join(', ');const existing=state.days[day];
    if(existing&&existing.text.trim()){
      if(existing.text.replace(/\s/g,'')!==txt.replace(/\s/g,''))existing.note='重複照片/人工原有資料與 OCR 不一致，請核對（最近辨識：'+txt+'）';
    }else state.days[day]={text:txt,note:'OCR 自動辨識，請人工核對',checked:false};
  }
 }catch(e){fail++;setStatus(`第 ${i+1} 張 OCR 失敗：${e.message}。仍可手動輸入。`,'warn');}
 renderRows();
 }
 setStatus(`OCR 完成：偵測到 ${scans} 個有時間的日期${fail?`；${fail} 張辨識失敗`:''}。請逐筆核對照片與時間。`,fail?'warn':'');state.pending=false;$('ocr').disabled=false;
});
function xml(s){return String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&apos;'}[c]));}
function colNo(n){let s='';for(;n;n=Math.floor((n-1)/26))s=String.fromCharCode(65+(n-1)%26)+s;return s;}
// Produce Office Open XML (.xlsx), ZIP stored entries; no dependencies or network.
const encoder=new TextEncoder();
const crcTable=(()=>{const a=[];for(let n=0;n<256;n++){let c=n;for(let j=0;j<8;j++)c=c&1?0xedb88320^(c>>>1):c>>>1;a.push(c>>>0);}return a})();
function crc32(bytes){let c=-1;for(const b of bytes)c=crcTable[(c^b)&255]^(c>>>8);return (c^-1)>>>0;}
function zipFiles(entries){const parts=[],central=[];let offset=0;
 for(const [name,raw] of entries){const bytes=encoder.encode(raw),nm=encoder.encode(name),crc=crc32(bytes);
 const h=new Uint8Array(30+nm.length),dv=new DataView(h.buffer);dv.setUint32(0,0x04034b50,true);dv.setUint16(4,20,true);dv.setUint16(6,0x0800,true);dv.setUint16(8,0,true);dv.setUint32(14,crc,true);dv.setUint32(18,bytes.length,true);dv.setUint32(22,bytes.length,true);dv.setUint16(26,nm.length,true);h.set(nm,30);parts.push(h,bytes);
 const ch=new Uint8Array(46+nm.length),cv=new DataView(ch.buffer);cv.setUint32(0,0x02014b50,true);cv.setUint16(4,20,true);cv.setUint16(6,20,true);cv.setUint16(8,0x0800,true);cv.setUint32(16,crc,true);cv.setUint32(20,bytes.length,true);cv.setUint32(24,bytes.length,true);cv.setUint16(28,nm.length,true);cv.setUint32(42,offset,true);ch.set(nm,46);central.push(ch);offset+=h.length+bytes.length;}
 const csize=central.reduce((x,e)=>x+e.length,0),end=new Uint8Array(22),e=new DataView(end.buffer);e.setUint32(0,0x06054b50,true);e.setUint16(8,central.length,true);e.setUint16(10,central.length,true);e.setUint32(12,csize,true);e.setUint32(16,offset,true);
 return new Blob([...parts,...central,end],{type:'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet'});
}
const num=(ref,val,style=0)=>`<c r="${ref}"${style?` s="${style}"`:''}><v>${val}</v></c>`;
const inline=(ref,val,style=0)=>`<c r="${ref}"${style?` s="${style}"`:''} t="inlineStr"><is><t xml:space="preserve">${xml(val)}</t></is></c>`;
const formula=(ref,f,cached,type='',style=0)=>`<c r="${ref}"${style?` s="${style}"`:''}${type?` t="${type}"`:''}><f>${xml(f)}</f>${cached===''?'<v/>':`<v>${cached}</v>`}</c>`;
function makeWorkbook(){let rows=[],s=0,totalActual=0,problem=0,confirmed=0;
 const year=+$('year').value,month=+$('month').value,monthN=daysInMonth();
 rows.push(`<row r="1" ht="37" customHeight="1">${inline('A1','打卡鐘每日工時統計表',1)}</row>`);
 rows.push(`<row r="2">${inline('A2','員工姓名',2)}${inline('B2',$('employee').value,3)}${inline('D2','月份',2)}${inline('E2',`${year} 年 ${pad(month)} 月（民國 ${year-1911} 年）`,3)}</row>`);
 rows.push(`<row r="3">${inline('A3','規則：當日排序配對；分鐘加總後，以每 15 分鐘 = 0.25 小時計算，不足 15 分鐘捨去。',4)}</row>`);
 const heads=['日期','開始時間','結束時間','原始打卡時間','實際分鐘','計算分鐘','工時（小時）','辨識狀態／備註','打卡 1','打卡 2','打卡 3','打卡 4'];
 rows.push(`<row r="5" ht="27" customHeight="1">${heads.map((v,i)=>inline(colNo(i+1)+'5',v,2)).join('')}</row>`);
 for(let d=1;d<=monthN;d++){
  const y=5+d,r=state.days[d]||{text:'',note:'',checked:false},cal=calculate(r.text),n=cal.punches.length,okay=n===2||n===4;
  const stampCols=cal.punches.map((p,i)=>num(colNo(i+9)+y,p/1440,6)).join('');
  const st=cal.type==='bad'?'需核對：'+cal.status:r.note&&!r.checked?'待核對：'+r.note:r.checked?'已核對':cal.status;
  const begin=cal.start?cal.punches[0]/1440:'';
  const end=cal.end?cal.punches[n-1]/1440:'';
  const fMinutes=`IF(COUNT(I${y}:L${y})=2,ROUND((MAX(I${y}:L${y})-MIN(I${y}:L${y}))*1440,0),IF(COUNT(I${y}:L${y})=4,ROUND(((SMALL(I${y}:L${y},2)-SMALL(I${y}:L${y},1))+(LARGE(I${y}:L${y},1)-LARGE(I${y}:L${y},2)))*1440,0),""))`;
  let line=inline(`A${y}`,`${pad(month)}/${pad(d)}`,3);
  line+=formula(`B${y}`,`IF(COUNT(I${y}:L${y})=0,"",MIN(I${y}:L${y}))`,begin,begin===''?'str':'',5);
  line+=formula(`C${y}`,`IF(COUNT(I${y}:L${y})=0,"",MAX(I${y}:L${y}))`,end,end===''?'str':'',5);
  line+=inline(`D${y}`,r.text||'',3);
  line+=formula(`E${y}`,fMinutes,cal.actual??'',okay?'':'str',3);
  line+=formula(`F${y}`,`IF(E${y}="","",INT(E${y}/15)*15)`,cal.rounded??'',okay?'':'str',3);
  line+=formula(`G${y}`,`IF(F${y}="","",F${y}/60)`,cal.hours??'',okay?'':'str',7);
  line+=inline(`H${y}`,st,r.note&&!r.checked||cal.type==='bad'?8:3);
  line+=stampCols;rows.push(`<row r="${y}" ht="24" customHeight="1">${line}</row>`);
  if(cal.hours!==null)s+=cal.hours;if(cal.actual!==null)totalActual+=cal.actual;if(cal.type==='bad')problem++;if(r.note&&!r.checked)confirmed++;
 }
 const last=5+monthN,t=last+2;
 rows.push(`<row r="${t}" ht="31" customHeight="1">${inline(`A${t}`,'全月合計',9)}${formula(`E${t}`,`SUM(E6:E${last})`,totalActual,'',9)}${formula(`F${t}`,`SUM(F6:F${last})`,s*60,'',9)}${formula(`G${t}`,`SUM(G6:G${last})`,s,'',10)}${inline(`H${t}`,`待補正 ${problem} 天；AI/OCR 待核對 ${confirmed} 天`,9)}</row>`);
 rows.push(`<row r="${t+2}">${inline(`A${t+2}`,'修改 I～L 欄的打卡時間，Excel 會重算工時與加總。',4)}</row>`);
 const sheet=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetViews><sheetView showGridLines="0" workbookViewId="0"/></sheetViews><sheetFormatPr defaultRowHeight="19"/><cols>${[13,15,15,31,15,15,17,55,13,13,13,13].map((w,i)=>`<col min="${i+1}" max="${i+1}" width="${w}" customWidth="1"/>`).join('')}</cols><sheetData>${rows.join('')}</sheetData><mergeCells count="2"><mergeCell ref="A1:L1"/><mergeCell ref="A3:L3"/></mergeCells><autoFilter ref="A5:L${last}"/><pageMargins left="0.3" right="0.3" top="0.5" bottom="0.5" header="0.2" footer="0.2"/></worksheet>`;
 const styles=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><numFmts count="2"><numFmt numFmtId="164" formatCode="hh:mm"/><numFmt numFmtId="165" formatCode="0.00"/></numFmts><fonts count="5"><font><sz val="11"/><name val="Microsoft JhengHei"/><color rgb="FF243748"/></font><font><b/><sz val="18"/><name val="Microsoft JhengHei"/><color rgb="FFFFFFFF"/></font><font><b/><sz val="11"/><name val="Microsoft JhengHei"/><color rgb="FFFFFFFF"/></font><font><sz val="11"/><name val="Microsoft JhengHei"/><color rgb="FF1166AA"/></font><font><sz val="11"/><name val="Microsoft JhengHei"/><color rgb="FFB46710"/></font></fonts><fills count="5"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF174C68"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FF246381"/></patternFill></fill><fill><patternFill patternType="solid"><fgColor rgb="FFE7F5F7"/></patternFill></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="11"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFill="1" applyFont="1"/><xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFill="1" applyFont="1"/><xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="164" fontId="3" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="165" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="0" fontId="4" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFill="1" applyFont="1"/><xf numFmtId="165" fontId="2" fillId="3" borderId="0" xfId="0" applyFill="1" applyFont="1"/></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>`;
 const content=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/><Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/><Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/></Types>`;
 const wb=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="每日工時" sheetId="1" r:id="rId1"/></sheets><calcPr calcId="191029" fullCalcOnLoad="1"/></workbook>`;
 const rel=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>`;
 const rootrel=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/><Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/></Relationships>`;
 const core=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:creator>打卡鐘工時統計</dc:creator></cp:coreProperties>`;
 const app=`<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties"><Application>Microsoft Office Excel</Application></Properties>`;
 return zipFiles([['[Content_Types].xml',content],['_rels/.rels',rootrel],['docProps/core.xml',core],['docProps/app.xml',app],['xl/workbook.xml',wb],['xl/_rels/workbook.xml.rels',rel],['xl/styles.xml',styles],['xl/worksheets/sheet1.xml',sheet]]);
}
$('download').addEventListener('click',()=>{
 const name=$('employee').value.trim()||'打卡',period=`${$('year').value}-${pad($('month').value)}`;
 const blob=makeWorkbook(),url=URL.createObjectURL(blob),a=document.createElement('a');a.href=url;a.download=`${period}_${name.replace(/[\\/:*?"<>|]/g,'_')}_工時統計.xlsx`;document.body.append(a);a.click();a.remove();setTimeout(()=>URL.revokeObjectURL(url),2000);
});
renderRows();
if(typeof module!=='undefined'&&module.exports)module.exports={parsePunches,calculate,zipFiles,makeWorkbook,extractTimes};
