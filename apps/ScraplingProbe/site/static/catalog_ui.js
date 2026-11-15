/* Browser-only prototype rule store. No customer data or API secrets. */
(() => {
  "use strict";
  const core=window.CatalogCore;
  const $=id=>document.getElementById(id);
  const data=$("catalog-data"),section=$("analysis");
  if(!core||!section||!data)return;
  let products=[];
  try{products=JSON.parse(data.textContent||"[]");}catch {return;}
  const STORAGE="momo-price-catalog-rules-v1";
  let rules={};
  try{const loaded=JSON.parse(localStorage.getItem(STORAGE)||"{}");if(loaded && !Array.isArray(loaded)&&typeof loaded==="object")rules=loaded;}catch{}
  const selections={},money=n=>n===null||n===undefined?"—":"NT$ "+n.toLocaleString("zh-TW",{maximumFractionDigits:2});
  const node=(tag,text,cls)=>{const el=document.createElement(tag);if(text!==undefined)el.textContent=String(text);if(cls)el.className=cls;return el;};
  const choose=(p)=>core.classification(p,rules["momo:"+p.code]);
  let effective=products.map(choose);
  const persist=()=>{try{localStorage.setItem(STORAGE,JSON.stringify(rules));return true;}catch{return false;}};
  function recompute(){effective=products.map(choose);render();}
  function makeButton(label,cb,cls=""){const b=node("button",label,cls);b.type="button";b.addEventListener("click",cb);return b;}
  function attributesText(attrs){return Object.entries(attrs||{}).map(([k,v])=>k+"="+v).join("\n");}
  function parseAttributes(str) {
    const attrs={};
    for (const line of str.split(/\r?\n/).slice(0,12)) {
      if(!line.trim())continue;
      const at=line.indexOf("=");if(at<1)throw Error("額外規格請使用「名稱=內容」，每行一項");
      const key=line.slice(0,at).trim(),value=line.slice(at+1).trim();
      if(!key||!value)throw Error("額外規格不可留空");
      attrs[key]=value;
    }
    return core.sanitizeAttributes(attrs);
  }
  function quickExclude(p) {
    if(p.stale){openEditor(p);return;}
    rules[p.id]={
      version:1,signature:p.signature,decision:p.excluded?"include":"exclude",
      model:p.model,quantity:p.quantity,unit:p.unit,attributes:p.attributes
    };
    if(!persist())alert("儲存失敗，請先檢查瀏覽器是否允許儲存網站資料。");
    recompute();
  }
  function openEditor(p){
    const dlg=$("catalog-dialog"),form=$("catalog-editor");
    $("edit-code").textContent=p.code;
    $("edit-title").textContent=p.title;
    $("edit-warning").textContent=p.stale?"商品標題已改變；請重新確認分類再儲存。":"";
    form.elements.model.value=p.model||"";
    form.elements.quantity.value=p.quantity??"";
    form.elements.unit.value=p.unit||"";
    form.elements.attributes.value=attributesText(p.attributes);
    form.elements.decision.value=p.excluded?"exclude":"include";
    $("edit-error").textContent="";
    const onSubmit=e=>{
      e.preventDefault();const model=form.elements.model.value.trim();
      const n=Number(form.elements.quantity.value);
      const unit=form.elements.unit.value.trim();
      let attrs;try{attrs=parseAttributes(form.elements.attributes.value)}catch(err){$("edit-error").textContent=err.message;return;}
      const decision=form.elements.decision.value;
      if(decision!=="exclude" && (!model || !unit || !Number.isFinite(n)||n<=0||n>100000)){
        $("edit-error").textContent="要納入單價統計，請確認型號、包裝數量及計價單位；不確定可先標記不使用。";
        return;
      }
      rules[p.id]={version:1,signature:p.signature,decision,model,quantity:n>0?n:null,unit,attributes:attrs};
      if(!persist()){$("edit-error").textContent="無法儲存到這個瀏覽器";return;}
      dlg.close();cleanup();recompute();
    };
    const onReset=()=>{delete rules[p.id];persist();dlg.close();cleanup();recompute();};
    const onClose=()=>cleanup();
    function cleanup(){form.removeEventListener("submit",onSubmit);$("edit-reset").removeEventListener("click",onReset);dlg.removeEventListener("close",onClose);}
    form.addEventListener("submit",onSubmit);
    $("edit-reset").addEventListener("click",onReset);
    dlg.addEventListener("close",onClose);
    dlg.showModal();
  }
  const attributesPreview=p=>Object.entries(p.attributes||{}).map(([k,v])=>k+"："+v).join("、")||"—";
  function renderFacets(){
    const host=$("catalog-facets");host.replaceChildren();
    const available=core.facets(effective);
    for(const facet of available){
      const wrap=node("label",facet.label,"facet");
      const select=node("select");select.setAttribute("aria-label",facet.label+"篩選");
      select.append(node("option","全部"));
      select.firstChild.value="";
      for(const value of facet.values){const opt=node("option",value);opt.value=value;select.append(opt);}
      if(!facet.values.includes(selections[facet.key]))delete selections[facet.key];
      select.value=selections[facet.key]||"";
      select.addEventListener("change",()=>{selections[facet.key]=select.value;renderRowsAndGroups();});
      wrap.append(select);host.append(wrap);
    }
    if(available.length)host.append(makeButton("清除篩選",()=>{for(const k of Object.keys(selections))delete selections[k];render();},"text-button"));
    else host.append(node("p","暫無可辨識的分類條件。人工補充分類後會自動出現在這裡。","dim"));
  }
  function renderRowsAndGroups() {
    const host=$("catalog-rows");host.replaceChildren();
    const rows=core.filtered(effective,selections);
    $("catalog-count").textContent="目前顯示 "+rows.length+" 筆；已排除 "+effective.filter(x=>x.excluded).length+" 筆；待確認 "+effective.filter(x=>x.stale||!x.ready&&!x.excluded).length+" 筆";
    for(const p of rows){
      const card=node("article",undefined,"catalog-row");
      if(p.excluded)card.classList.add("is-excluded");
      const left=node("div",undefined,"catalog-main");
      const headline=node("div",undefined,"catalog-model");
      headline.append(node("strong",p.model||"未辨識型號"));
      headline.append(node("span",p.status,"catalog-tag"));
      left.append(headline);
      left.append(node("div","規格："+attributesPreview(p)+"　·　包裝："+(p.quantity?(p.quantity+" "+p.unit):"未確認"),"catalog-spec"));
      left.append(node("div",p.title,"catalog-original"));
      const right=node("div",undefined,"catalog-price");
      right.append(node("strong",money(p.price),"catalog-amount"));
      right.append(node("div",p.unitPrice===null?"單價待確認":money(p.unitPrice)+"／"+p.unit,"catalog-unit-price"));
      const actions=node("div",undefined,"catalog-actions");
      actions.append(makeButton(p.stale?"重新確認":"編輯分類",()=>openEditor(p)));
      actions.append(makeButton(p.excluded?"取消排除":"不使用",()=>quickExclude(p),"text-button"));
      right.append(actions);card.append(left,right);host.append(card);
    }
    if(!rows.length)host.append(node("p","沒有符合目前篩選的商品。","empty"));
    renderSummary(rows);
  }
  function renderSummary(rows) {
    const host=$("catalog-groups");host.replaceChildren();
    const summary=core.groups(rows.filter(x=>!x.excluded&&!x.stale));
    $("group-count").textContent=summary.length+" 個可比較群組（使用每單位推算價；不含待確認／已排除）";
    if(!summary.length){host.append(node("p","目前沒有具備明確型號、包裝數量與價格的可比群組。可以編輯分類後重新計算。","dim"));return;}
    for(const g of summary){
      const st=g.statistics,section=node("article",undefined,"group-card");
      section.append(node("h4",g.model+" ｜每 "+g.unit+" 價格","group-title"));
      section.append(node("p",attributesPreview({attributes:g.attributes}),"group-spec"));
      const metrics=node("div",undefined,"group-metrics");
      for(const [name,val] of [["筆數",st.count],["最低價",money(st.min)],["最高價",money(st.max)],["平均價",money(st.mean)],["中位數",money(st.median)],["樣本標準差",st.stddev===null?"至少兩筆才計算":money(st.stddev)]]) {
        const box=node("div",undefined,"metric");
        box.append(node("span",name),node("strong",val));metrics.append(box);
      }
      section.append(metrics);
      if(st.count>1){
        const chart=node("div",undefined,"range-chart");
        for(const entry of g.items){
          const line=node("div",undefined,"range-row");
          const label=node("span",entry.code);
          const track=node("div",undefined,"range-track");
          const marker=node("span",undefined,"range-marker");
          const span=st.max-st.min;
          marker.style.left=((span?((entry.unitPrice-st.min)/span)*94:47)+3)+"%";
          marker.title=money(entry.unitPrice)+"／"+g.unit;
          track.append(marker);line.append(label,track,node("span",money(entry.unitPrice)));
          chart.append(line);
        }
        section.append(chart);
      }
      host.append(section);
    }
  }
  function render(){renderFacets();renderRowsAndGroups();}
  function exportRules(){
    const exportData={type:"momo-price-prototype-rules",version:1,exportedAt:new Date().toISOString(),rules};
    const blob=new Blob([JSON.stringify(exportData,null,2)],{type:"application/json"});
    const url=URL.createObjectURL(blob),a=node("a");a.href=url;a.download="momo-catalog-rules.json";a.click();setTimeout(()=>URL.revokeObjectURL(url),500);
  }
  function importRules(file){
    if(!file || file.size>500000){alert("檔案太大或無效");return;}
    file.text().then(text=>{
      const data=JSON.parse(text);
      if(data.type!=="momo-price-prototype-rules"||data.version!==1||typeof data.rules!=="object"||!data.rules||Array.isArray(data.rules))
        throw Error("不是本系統匯出的分類備份");
      if(Object.keys(data.rules).length>3000)throw Error("分類數量超過限制");
      if(!confirm("匯入會取代這個瀏覽器現有的全部人工分類。確定繼續？"))return;
      const accepted={};
      for(const [key,rule] of Object.entries(data.rules)){
        if(!/^momo:(?:\d{5,14}|TP\d{10,18})$/.test(key) || !rule || rule.version!==1 || !/^[\p{L}\p{N}]{1,400}$/u.test(rule.signature||""))continue;
        accepted[key]={version:1,signature:rule.signature,decision:rule.decision==="exclude"?"exclude":"include",
          model:core.normalize(rule.model).slice(0,80),quantity:Number(rule.quantity)||null,
          unit:core.normalize(rule.unit).slice(0,20),attributes:core.sanitizeAttributes(rule.attributes)};
      }
      rules=accepted;if(!persist())throw Error("瀏覽器儲存失敗");
      recompute();
    }).catch(e=>alert("匯入失敗："+e.message));
  }
  $("export-rules").addEventListener("click",exportRules);
  $("import-rules").addEventListener("change",e=>{importRules(e.target.files?.[0]);e.target.value="";});
  $("catalog-dialog").addEventListener("click",e=>{if(e.target===$("catalog-dialog"))e.target.close();});
  // The original server-rendered list stays as a no-JavaScript fallback.
  document.querySelector(".table")?.classList.add("raw-fallback-hidden");
  section.hidden=false;
  render();
})();
