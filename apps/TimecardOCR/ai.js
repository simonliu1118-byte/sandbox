'use strict';
// Browser-side UI only. The API key lives in the password field until the browser tab closes.
let aiCandidates=[];
let aiBusy=false;
function clearAIResults(){aiCandidates=[];$('ai-result').hidden=true;$('ai-result-rows').innerHTML='';}
function aiGridCrop(card){
 const c=card.coords,img=card.image,sx=img.naturalWidth/750,sy=img.naturalHeight/1000;
 // Exclude name, ID, company, and month at top. Include only printed date and time grid.
 const x=Math.max(0,Math.floor((c.left-38)*sx)),y=Math.max(0,Math.floor((c.top-27)*sy));
 const right=Math.min(img.naturalWidth,Math.ceil((c.right+19)*sx));
 const bottom=Math.min(img.naturalHeight,Math.ceil((c.bottom+17)*sy));
 if(right-x<20||bottom-y<60)throw Error('裁切範圍無效，請調整照片格線座標。');
 const targetW=Math.min(1100,Math.max(550,Math.round((right-x)*2.9)));
 const canvas=document.createElement('canvas');canvas.width=targetW;canvas.height=Math.min(3000,Math.round(targetW*(bottom-y)/(right-x)));
 const context=canvas.getContext('2d');context.imageSmoothingEnabled=true;context.imageSmoothingQuality='high';context.fillStyle='#fff';context.fillRect(0,0,canvas.width,canvas.height);
 context.drawImage(img,x,y,right-x,bottom-y,0,0,canvas.width,canvas.height);
 return canvas;
}
function previewAICrop(card){
 const canvas=aiGridCrop(card);
 $('modalimg').src=canvas.toDataURL('image/jpeg',.87);$('modal').style.display='flex';
}
function normalizeAIRows(rows,side){
 const from=side==='front'?1:16,to=side==='front'?15:31;
 const byDay=new Map();
 if(!Array.isArray(rows))return [];
 for(const r of rows){
  if(!r||typeof r!=='object')continue;
  const day=Number(r.day);if(!Number.isInteger(day)||day<from||day>to||day>daysInMonth())continue;
  const times=[];
  for(const raw of (Array.isArray(r.punches)?r.punches:[]).slice(0,7)){
   let t=String(raw||'').trim().replace(/[：.]/g,':');
   if(/^\d{3,4}$/.test(t))t=t.slice(0,-2)+':'+t.slice(-2);
   try{const result=parsePunches(t);if(result.length===1)times.push(timeStr(result[0]));}catch(_){}
  }
  const unique=[...new Set(times)].sort((a,b)=>a.localeCompare(b));
  if(unique.length===0)continue;
  // Do not accept more than four stamps automatically, nor infer missing counterparts.
  const candidate={day,punches:unique,text:unique.join(', '),uncertain:r.uncertain!==false,note:String(r.note||'').slice(0,180)};
  const old=byDay.get(day);
  if(!old||candidate.punches.length>old.punches.length)byDay.set(day,candidate);
 }
 return [...byDay.values()].sort((a,b)=>a.day-b.day);
}
function showAIResults(rows,filename,side){
 aiCandidates=normalizeAIRows(rows,side);
 $('ai-result').hidden=false;
 $('ai-result-info').textContent=`來源：${filename}｜讀取 ${aiCandidates.length} 天有印字的日期。未讀出的時間不會推算；有疑慮的日期請對照原圖。`;
 $('ai-result-rows').innerHTML=aiCandidates.length?aiCandidates.map((r,i)=>{
   const existing=state.days[r.day]||{text:'',checked:false};
   const c=calculate(existing.text),valid=calculate(r.text);
   const safeDefault=(!existing.text.trim()||c.type==='bad')&&(valid.type==='ok');
   const warning=[r.uncertain?'AI 標示不確定':'',valid.type!=='ok'?`僅 ${r.punches.length} 筆，須補齊`:'',r.note].filter(Boolean).join('；');
   return `<tr><td><input type="checkbox" class="ai-choose" data-ai-index="${i}" ${safeDefault?'checked':''}></td><td>${pad(+$('month').value)}/${pad(r.day)}</td><td>${esc(existing.text||'（空白）')}</td><td>${esc(r.text)}</td><td class="${warning?'ai-uncertain':''}">${esc(warning||'請核對原圖')}</td></tr>`;
 }).join(''):'<tr><td colspan="5">沒有可靠可讀取的時間。請調整裁切位置、再試一次，或手動輸入。</td></tr>';
 $('ai-result').scrollIntoView({behavior:'smooth',block:'start'});
}
$('ai-dismiss').addEventListener('click',clearAIResults);
$('ai-apply').addEventListener('click',()=>{
 const inputs=[...document.querySelectorAll('.ai-choose:checked')];let applied=0;
 for(const node of inputs){const r=aiCandidates[Number(node.dataset.aiIndex)];if(!r)continue;
  const current=state.days[r.day]||{text:'',note:'',checked:false};
  if(current.text.trim()&&current.checked){
   if(!confirm(`${r.day} 日已由人工核對，確定以 AI 建議覆蓋嗎？`))continue;
  }
  state.days[r.day]={text:r.text,checked:false,note:'Gemini AI 補強辨識（尚未人工核對）'+(r.uncertain?'；AI 標示不確定':'')+(r.note?'；'+r.note:'')};
  applied++;
 }
 clearAIResults();renderRows();setStatus(`已套用 ${applied} 天 AI 建議。請逐日對照照片後勾選「已核對」。`,applied?'warn':'');
});
$('cards').addEventListener('click',async e=>{
 const preview=e.target.closest('.ai-preview');if(preview){
  const c=state.cards[Number(preview.dataset.card)];if(c)try{previewAICrop(c);}catch(err){alert(err.message);}return;
 }
 const trigger=e.target.closest('.ai-scan');if(!trigger)return;
 const card=state.cards[Number(trigger.dataset.card)];if(!card||aiBusy||state.pending)return;
 const key=$('ai-key').value.trim();if(!key){alert('請先輸入 Gemini API Key。');$('ai-key').focus();return;}
 if(!$('ai-consent').checked){alert('請先勾選資料傳送同意，並預覽裁切範圍。');return;}
 const model=$('ai-model').value;
 if(!['gemini-3.5-flash-lite','gemini-3.6-flash'].includes(model))return;
 let canvas;try{canvas=aiGridCrop(card);}catch(err){alert(err.message);return;}
 aiBusy=true;trigger.disabled=true;trigger.textContent='AI 辨識中…';setStatus(`正在使用 Gemini AI 辨識 ${card.f.name}；只傳送打卡表格裁切區域…`);
 try{
  const image=canvas.toDataURL('image/jpeg',.86).split(',')[1];
  const response=await fetch('/api/ai',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({image,apiKey:key,model,side:card.side})});
  let result;try{result=await response.json();}catch(_){throw Error(`API 回應格式錯誤 (HTTP ${response.status})`)}
  if(!response.ok||!result.ok)throw Error(result.error||`HTTP ${response.status}`);
  showAIResults(result.rows||[],card.f.name,card.side);
  setStatus(`AI 辨識完成：${(result.rows||[]).length} 天有候選結果。請在「AI 候選結果」勾選想採用的日期並逐日核對。`);
 }catch(err){setStatus(`AI 失敗：${err.message}。未修改任何工時；可繼續離線 OCR 或人工輸入。`,'bad');}
 finally{aiBusy=false;trigger.disabled=false;trigger.textContent='AI 辨識此張（需同意）';}
});
if(typeof module!=='undefined'&&module.exports)module.exports={aiGridCrop,normalizeAIRows};
