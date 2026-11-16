// data/map/block_rooms.json을 판정 도구가 쓰는 형태(s1·s2 목록)로 읽는다.
(function(root){
 'use strict';
 const src=typeof module!=='undefined'?require('../../data/map/block_rooms.json'):root.BLOCK_ROOMS;
 const fix=list=>list.map(x=>x.need&&x.need.length?x:{...x,need:undefined});
 const conv=r=>({...r,from:r.source,solids:fix(r.solids),plats:fix(r.plats)});
 const pick=(stage,special)=>src.rooms.filter(r=>r.stage===stage&&(r.kind!=='combat')===special).map(conv);
 const connectors=[
  {id:'H',name:'수평 직선',openings:['L','R']},{id:'LT',name:'굴곡(좌→위)',openings:['L','T']},{id:'RB',name:'굴곡(우→아래)',openings:['R','B']},
  {id:'LRT',name:'T자 분기',openings:['L','R','T']},{id:'LRB',name:'T자 분기(아래)',openings:['L','R','B']},{id:'X',name:'십자',openings:['L','R','T','B']},
  {id:'VC',name:'벽타기 수직',openings:['T','B']}];
 const api={s1:pick(1,false),s1special:pick(1,true),s2:pick(2,false),s2special:pick(2,true),connectors,species:src.species,spec:src.spec};
 if(typeof module!=='undefined')module.exports=api;else root.RoomData=api;
})(typeof window!=='undefined'?window:globalThis);
