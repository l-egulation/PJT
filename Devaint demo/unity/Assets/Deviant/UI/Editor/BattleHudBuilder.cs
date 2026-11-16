using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using TMPro;
using Deviant.World;
using Deviant.UI;

namespace Deviant.UI.Editor
{
[Serializable] public class HudLayout {public int[] referenceResolution;public HudOperation[] operations;}
[Serializable] public class HudOperation {public string type,id,text,font,color;public int x,y,w,h,fontSize;}
public static class BattleHudBuilder
{
    public const string Root="Assets/Deviant/UI";
    public const string ScenePath="Assets/Deviant/World/Scenes/TwilightInkHUDTest.unity";
    public const string LayoutScene="Assets/Deviant/World/Scenes/TwilightInkHUDLayoutTest.unity";
    public const string PrefabPath=Root+"/Prefabs/BattleHUDv2.prefab";
    static string Output=>Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
    [MenuItem("Tools/deviant/UI/Rebuild HUD Prefab")]
    public static void BuildAndVerify() { Build(); }
    public static void Build()
    {
        HudKoreanFontBuilder.Build();
        Directory.CreateDirectory(Root+"/Prefabs");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach(string png in Directory.GetFiles(Root+"/Sprites","*.png"))((TextureImporter)AssetImporter.GetAtPath(png.Replace('\\','/'))).SaveAndReimport();
        var korean = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HudKoreanFontBuilder.AssetPath);
        var fonts=new[]{korean,korean,korean};
        var layout=JsonUtility.FromJson<HudLayout>(File.ReadAllText(Root+"/Data/layout.json"));
        var root=new GameObject("BattleHUDv2",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;canvas.pixelPerfect=true;
        var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1672,941);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
        var source=root.AddComponent<BattleHudDataSource>();var view=root.AddComponent<BattleHudView>();view.dataSource=source;
        var status=Group(root.transform,"TopLeft_Status",new Vector2(0,1),new Vector2(0,1),new Vector2(39,-38),new Vector2(448,169));
        var right=Group(root.transform,"TopRight_CurrencyControls",new Vector2(1,1),new Vector2(1,1),new Vector2(-39,-31),new Vector2(340,125));
        var loadout=Group(root.transform,"BottomCenter_Loadout",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-1.5f,24),new Vector2(631,123));
        var inventory=Group(root.transform,"BottomRight_Inventory",new Vector2(1,0),new Vector2(1,0),new Vector2(-20,27),new Vector2(342,68));
        view.loadoutGroup=loadout;view.inventoryGroup=inventory;
        var inserts=new System.Collections.Generic.List<Image>();var drops=new System.Collections.Generic.List<Image>();
        int index=0;
        foreach(var op in layout.operations)
        {
            RectTransform group;int ox,oy;
            if(op.y>700&&op.x>1200){group=inventory;ox=1310;oy=846;}
            else if(op.y>700){group=loadout;ox=519;oy=794;}
            else if(op.x>1200){group=right;ox=1293;oy=31;}
            else{group=status;ox=39;oy=38;}
            var rect=Rect(group,(op.id??op.text)+"_"+index++,op.x-ox,op.y-oy,op.w,op.h);
            if(op.type=="sprite")
            {
                if(op.id=="hp_fill"||op.id=="exp_fill")
                {
                    var gauge=rect.gameObject.AddComponent<PreservedGauge>();gauge.maxWidth=301;gauge.height=op.h;
                    gauge.mask=Rect(rect,"RatioClip",0,0,op.w,op.h);gauge.mask.gameObject.AddComponent<RectMask2D>();
                    gauge.fill=Rect(gauge.mask,"SlicedFill_Caps6px",0,0,op.w,op.h);var img=gauge.fill.gameObject.AddComponent<Image>();img.sprite=Sprite(op.id);img.type=Image.Type.Sliced;img.raycastTarget=false;
                    if(op.id=="hp_fill")view.hpGauge=gauge;else view.expGauge=gauge;continue;
                }
                var image=rect.gameObject.AddComponent<Image>();image.sprite=Sprite(op.id);image.preserveAspect=true;image.raycastTarget=false;
                if(op.id.StartsWith("ink_insert"))inserts.Add(image);if(op.id.StartsWith("drop_"))drops.Add(image);
                if(op.id=="pause_frame")view.pauseButton=Button(image);
                if(op.id=="escape_frame")view.escapeButton=Button(image);
                if(op.id=="bag_frame")view.bagButton=Button(image);
                if(op.id=="mix_frame")view.mixButton=Button(image);
            }
            else
            {
                var font=fonts[op.font=="arial.ttf"?0:op.font=="malgunbd.ttf"?2:1];var text=Text(rect,op.text,font,op.fontSize);
                ColorUtility.TryParseHtmlString(op.color,out var color);text.color=color;
                if(op.text=="70 / 100")view.hpText=text;if(op.text=="40 / 100")view.expText=text;
                if(op.text=="Lv.3")view.levelText=text;if(op.text=="125")view.coinText=text;if(op.text=="38")view.gemText=text;
                if(op.text=="깃털펜")view.weaponText=text;if(op.text=="3 / 3")view.capacityText=text;if(op.text=="가방 4 / 6")view.bagText=text;if(op.text=="숲 · 1-2")view.regionText=text;
            }
        }
        view.inserts=inserts.ToArray();view.drops=drops.ToArray();view.dropRed=Sprite("drop_red");view.dropBlue=Sprite("drop_blue");
        view.insertRed=new[]{Sprite("ink_insert_red"),Sprite("ink_insert_red_b"),Sprite("ink_insert_red_b")};view.insertBlue=new[]{Sprite("ink_insert_blue"),Sprite("ink_insert_blue"),Sprite("ink_insert_blue")};
        // Alternative colors share a slot's native dimensions; preserveAspect keeps the extracted art proportional.
        var dialog=Group(root.transform,"ActionDialog",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(570,250));
        var background=dialog.gameObject.AddComponent<Image>();background.sprite=Sprite("loadout_frame");background.type=Image.Type.Sliced;view.dialog=dialog.gameObject;
        view.dialogTitle=Text(Rect(dialog,"Title",28,22,510,38),"일시정지",fonts[2],26);
        view.dialogBody=Text(Rect(dialog,"Body",28,70,510,96),"",fonts[1],22);view.dialogBody.textWrappingMode=TextWrappingModes.Normal;
        var close=Rect(dialog,"Close",212,185,146,45);var closeImage=close.gameObject.AddComponent<Image>();closeImage.sprite=Sprite("mix_frame");closeImage.type=Image.Type.Sliced;view.closeButton=Button(closeImage);
        Text(Rect(close,"Label",18,7,110,31),"닫기",fonts[2],22).alignment=TextAlignmentOptions.Center;dialog.gameObject.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Deviant.Game.Editor.GameBuilder.Open();
    }

    static Sprite Sprite(string id)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Sprites/"+id+".png");
    static Button Button(Image image){image.raycastTarget=true;var b=image.gameObject.AddComponent<Button>();b.targetGraphic=image;b.transition=Selectable.Transition.None;return b;}
    static TMP_Text Text(RectTransform rect,string value,TMP_FontAsset font,int size)
    {
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMin=size-4;text.fontSizeMax=size;
        text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
        // The approved compositor centers the actual glyph bounds in each layout rectangle.
        text.alignment=TextAlignmentOptions.MidlineGeoAligned;text.color=new Color32(243,239,223,255);text.raycastTarget=false;return text;
    }
    static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
    {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
    static RectTransform Group(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {var r=Rect(parent,name,0,0,size.x,size.y);r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;return r;}
    [MenuItem("Tools/deviant/UI/Play Common Game")] public static void Play(){Deviant.Game.Editor.GameBuilder.Play();}
    public static void SetGameResolution(int width,int height)
    {
        var asm=typeof(EditorWindow).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
        var singleton=sizesType.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        var groupType=asm.GetType("UnityEditor.GameViewSizeGroupType");var group=sizesType.GetMethod("GetGroup").Invoke(singleton,new[]{Enum.Parse(groupType,"Standalone")});
        var sizeType=asm.GetType("UnityEditor.GameViewSize");var kindType=asm.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{Enum.Parse(kindType,"FixedResolution"),width,height,"HUD "+width+"×"+height},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));view.GetType().GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,count-1);view.Repaint();
    }
}
}
