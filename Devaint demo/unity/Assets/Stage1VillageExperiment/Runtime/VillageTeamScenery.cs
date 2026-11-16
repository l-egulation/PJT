using System;
using System.Collections.Generic;
using ForestCombatV2;
using UnityEngine;

namespace Stage1VillageExperiment
{
    // Decorative placements derived from the team's final occupancy. No new solids,
    // platforms, doors, encounters, routes or movement settings are introduced here.
    public static class VillageTeamScenery
    {
        // A deck draws its top on its row and its body and hanging leaves about three rows below it.
        const int DeckArtRows=4;
        public static VillageArtLayout Create(ForestData data)
        {
            var props=new List<VillageProp>();
            bool Solid(int x,int y)=>x>=0&&y>=0&&x<data.cols&&y<data.rows&&data.ground[y*data.cols+x]!=0;
            float start=float.MaxValue,end=float.MinValue;
            foreach(var room in data.rooms??Array.Empty<ForestRoom>())
            {start=Mathf.Min(start,room.x);end=Mathf.Max(end,room.x+room.w);}
            foreach(var room in data.rooms??Array.Empty<ForestRoom>())
            {
                // The clock arena owns its rear wall and machinery. Village trees,
                // fountains and shrubs must not appear in front of that interior.
                if(room.kind=="boss")continue;
                if(room.authoredDecorations!=null)
                {
                    foreach(var d in room.authoredDecorations)
                    {
                        if(!d.assetId.StartsWith("village:"))continue;
                        props.Add(new VillageProp{kind=d.assetId.Substring(8),x=(room.x+(room.mirrored?room.w-d.x:d.x))*32,
                            y=(room.y+d.y)*32,width=d.width*32,height=d.height*32,sortingOrder=d.order,
                            flipX=d.flipX^room.mirrored,rotation=room.mirrored?-d.rotation:d.rotation,zone=room.id});
                    }
                    continue;
                }
                // Districts follow horizontal stage progression, including upper/lower
                // branches at the same station. They do not change room gameplay roles.
                float progress=((room.x+room.w*.5f)-start)/Mathf.Max(1,end-start);
                int district=Mathf.Min(3,Mathf.FloorToInt(progress*4));
                string primary=district==0?(room.id%3==1?"house_balcony":"phase2_residential"):
                    district==1?"phase2_market":district==2?"phase2_plaza":"phase2_tree";
                bool Fits(string kind,float center,int floor,float scale)
                {
                    var image=Resources.Load<Texture2D>("Village/color/props/"+kind);
                    if(!image)throw new InvalidOperationException("Missing scenery: "+kind);
                    int left=Mathf.FloorToInt((center-image.width*scale*.5f)/32);
                    int right=Mathf.CeilToInt((center+image.width*scale*.5f)/32);
                    int h=Mathf.CeilToInt(image.height*scale/32);
                    if(left<room.x+1||right>room.x+room.w-1||floor-h<room.y)return false;
                    // Gimmick marks (seal tablet, chest) stay clear of scenery so they read at a glance.
                    foreach(var mark in data.landmarks??Array.Empty<ForestLandmark>())
                        if((mark.kind=="seal"||mark.kind=="chest")&&mark.zone==room.id&&mark.x>left*32-64&&mark.x<right*32+64)return false;
                    // Structures never sit behind a deck: decks and their hanging art stay readable (user request 2026-11-06).
                    foreach(var p in data.platforms??Array.Empty<FoliagePlatform>())
                    {
                        int px0=p.x/32-1,px1=(p.x+p.w)/32+1,row=p.y/32;
                        if(px0<right&&left<px1&&row-1<floor&&floor-h<row+DeckArtRows)return false;
                    }
                    for(int x=left;x<right;x++)
                    {
                        if(!Solid(x,floor))return false;
                        for(int dy=1;dy<=h;dy++)if(Solid(x,floor-dy))return false;
                    }
                    return true;
                }
                void Add(string kind,float center,int floor,float scale,int order)=>
                    props.Add(new VillageProp{kind=kind,x=center,y=floor*32,scale=scale,zone=room.id,sortingOrder=order});
                void Companion(string kind,float preferred,int floor,float scale,int order)
                {
                    // Decorative drawings may overlap. They must still stand on
                    // the same supported floor, with no terrain passing through them.
                    foreach(int offset in new[]{0,32,-32,64,-64})
                        if(Fits(kind,preferred+offset,floor,scale)){Add(kind,preferred+offset,floor,scale,order);return;}
                }
                var primaryImage=Resources.Load<Texture2D>("Village/color/props/"+primary);
                int pw=Mathf.CeilToInt(primaryImage.width/32f),ph=Mathf.CeilToInt(primaryImage.height/32f);
                bool found=false;
                for(int y=room.y+room.h-1;y>=room.y+ph&&!found;y--)
                for(int x=room.x+4;x+pw<=room.x+room.w-4&&!found;x++)
                {
                    float center=(x+pw*.5f)*32;
                    if(!Fits(primary,center,y,1))continue;
                    Add(primary,center,y,1,-24);found=true;
                    Companion(district==2?"phase2_cypress":"phase2_tree",center-112,y,district==0?1.2f:1,-43);
                    Companion("courtyard_wall",center+156,y,1,-34);
                    if(district==3)Companion("phase2_plaza",center+176,y,1,-22);
                    Companion("phase2_shrub",center+primaryImage.width*.43f,y,.88f,-12);
                    Companion("phase2_shrub",center-primaryImage.width*.43f,y,.64f,-13);
                }
                // Small, sparse ornaments only. No collision and no covering seams.
                int lastGrass=-100;
                for(int x=room.x+2;x<room.x+room.w-2;x++)
                {
                    if(x-lastGrass<4)continue;
                    uint hash=unchecked((uint)(x*73856093^room.id*19349663));
                    if(hash%7>1)continue;
                    for(int y=room.y+1;y<room.y+room.h;y++)
                    {
                        if(!Solid(x,y)||Solid(x,y-1)||!Solid(x+1,y)||Solid(x+1,y-1))continue;
                        props.Add(new VillageProp{kind="phase2_grass",x=(x+.5f)*32,y=y*32,scale=.7f,zone=room.id,sortingOrder=1});
                        lastGrass=x;break;
                    }
                }
            }
            return new VillageArtLayout{props=props.ToArray(),undergroundRects=Array.Empty<VillageRoom>(),
                shaftRects=Array.Empty<VillageRoom>(),waterAnchors=Array.Empty<VillageProp>(),
                lanterns=Array.Empty<VillageProp>(),aqueducts=Array.Empty<VillageProp>(),fountainAnchors=Array.Empty<VillageProp>()};
        }
    }
}
