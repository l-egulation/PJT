// Preview layout in canvas pixels. Unity placement is intentionally separate.
export const journeySpec={
 floorY:615,pixelsPerUnit:1280/30,
 corridorEnd:4200,roomStart:4200,roomWidth:1280,worldWidth:5480,
 cameraTravel:4200,travelSeconds:36,
 stages:[
  {progress:0,id:'village',name:'마을의 끝',description:'열린 마을 풍경 사이로 낡은 벽의 흔적이 보입니다.'},
  {progress:.22,id:'fragments',name:'벽의 흔적',description:'회벽 조각과 중경 땅이 마을 풍경을 조금씩 가립니다.'},
  {progress:.46,id:'tunnel',name:'좁은 굴길',description:'벽이 점점 이어지고, 바깥 풍경은 작은 틈으로만 보입니다.'},
  {progress:.66,id:'machinery',name:'시계실 앞',description:'벽이 길 전체를 감싸고, 톱니와 도르레가 보스방을 암시합니다.'},
  {progress:1,id:'chamber',name:'보스방 · 시계실',description:'낮은 천장이 끝나고, 같은 석재와 시계 장치가 큰 공간으로 이어집니다.'}
 ],
 collision:'Preview only. All added walls and machinery are rear decoration; existing floor is preserved.',
 artSource:'./assets; native image edits referenced directly from existing Unity village house, wall and arch',
 bossRoomSource:'Flat pencil-drawn rear wall; shared corridor machinery; V10 geometry and original game floor/actors preserved.'
};
