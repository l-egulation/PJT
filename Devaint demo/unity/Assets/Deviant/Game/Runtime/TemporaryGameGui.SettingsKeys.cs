using System;
using System.Collections.Generic;
using System.Linq;
using Deviant.Input.Contracts;
using Deviant.Input.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Deviant.Game
{
    public sealed partial class TemporaryGameGui
    {
        bool shortcutsInSettings;
        ScrollRect inlineShortcutScroll;
        readonly Dictionary<Text,Image> inlineKeyRows=new Dictionary<Text,Image>();

        void DisposeInlineShortcuts()
        {
            if(!shortcutsInSettings)return;
            CloseShortcuts();
            shortcutPanel=null;shortcutStatus=null;shortcutCancel=null;
            shortcutsInSettings=false;inlineShortcutScroll=null;
            shortcutKeys.Clear();inlineKeyRows.Clear();
        }

        void BuildInlineShortcuts(LocalInput input)
        {
            shortcutsInSettings=true;shortcutPanelOpen=true;
            shortcutPanel=Rect("Inline control settings",settingsBody,0,0,605,330).gameObject;
            var content=JournalScroll("Controls scroll",shortcutPanel.transform,-12,0,617,250);
            inlineShortcutScroll=content.parent.GetComponent<ScrollRect>();
            float y=5;
            BuildInlineKeySection(input,content,"이동",ShortcutPages[0].Take(6),ref y);
            BuildInlineKeySection(input,content,"전투",ShortcutPages[0].Skip(8).Take(4).Concat(ShortcutPages[0].Skip(6).Take(1)),ref y);
            BuildInlineKeySection(input,content,"탐험",ShortcutPages[0].Skip(7).Take(1),ref y,false);
            BuildFixedInlineKey(content,"보상 열기","G",y);y+=32;
            foreach(var row in ShortcutPages[0].Skip(12)) {BuildInlineKeyRow(input,row,content,y);y+=32;}
            y+=22.5f;
            BuildInlineKeySection(input,content,"메뉴",ShortcutPages[1].Take(9),ref y);
            BuildInlineKeySection(input,content,"지도",ShortcutPages[1].Skip(9).Take(4),ref y);
            BuildInlineKeySection(input,content,"기타",ShortcutPages[1].Skip(13),ref y);
            content.sizeDelta=new Vector2(617,y);
            shortcutStatus=Words(ShortcutHint,shortcutPanel.transform,0,261,490,19,11,false,JMuted);
            Words("아래로 스크롤 ↓",shortcutPanel.transform,500,261,105,19,11,false,JMuted,TextAnchor.MiddleRight);
            Fill(shortcutPanel.transform,0,289,605,.5f,new Color32(168,175,182,43));
            var reset=ApprovedKeyAction("기본 키로 복원","G",shortcutPanel.transform,0,302,120,25,()=>OpenShortcutResetQuestion(ApplyShortcutDefaults),16,18);
            reset.name="Restore default keys";
            Words("키보드",shortcutPanel.transform,128,302,80,25,12,false,JMuted);
            shortcutCancel=SettingsAction("입력 취소",shortcutPanel.transform,495,302,110,25,CancelShortcutEdit,14);
            shortcutCancel.gameObject.SetActive(false);
            if(input)RefreshShortcutKeys(input);
        }

        void BuildInlineKeySection(LocalInput input,RectTransform content,string name,IEnumerable<ShortcutRow> rows,ref float y,bool gap=true)
        {
            if(y>5)Fill(content,12,y-12,584.5f,.5f,new Color32(168,175,182,43));
            Words(name,content,12,y,400,23,17,false,JInk);
            y+=28.5f;
            foreach(var row in rows){BuildInlineKeyRow(input,row,content,y);y+=32;}
            if(gap)y+=22.5f;
        }

        void BuildFixedInlineKey(RectTransform parent,string name,string key,float y)
        {
            Words(name,parent,12,y,430,32,14,false,JInk);
            SettingsKeycap(parent,540,y+5.5f,key,34,20.5f,14);
        }

        void BuildInlineKeyRow(LocalInput input,ShortcutRow row,RectTransform parent,float y)
        {
            var rowTitle=Words(row.Label,parent,12,y,430,32,14,false,JInk);
            if(!input)return;
            var action=input.Source.GetAction(row.Action);if(action==null)return;
            var indices=Enumerable.Range(0,action.bindings.Count).Where(i=>KeyboardShortcutSettings.IsKeyboard(action.bindings[i])&&
                (row.Direction==null||string.Equals(row.Direction,action.bindings[i].name,StringComparison.OrdinalIgnoreCase))).ToArray();
            for(int column=0;column<indices.Length;column++)
            {
                int index=indices[column];
                float width=indices.Length>1?65:80,x=596.5f-(indices.Length-column)*(width+4);
                var button=SettingsAction("",parent,x,y,width,32,()=>StartShortcutEdit(row,index),14,TextAnchor.MiddleCenter);
                button.name="Binding "+row.Action+" "+index;
                RegisterSettingsRow(rowTitle,button);
                var label=button.GetComponentInChildren<Text>();
                label.resizeTextForBestFit=true;label.resizeTextMinSize=11;label.resizeTextMaxSize=14;
                var border=Rounded("Binding keycap",button.transform,(width-34)/2,5.5f,34,20.5f,2,1,Color.clear,new Color32(147,156,166,200));
                border.raycastTarget=false;border.transform.SetAsFirstSibling();
                var rowWash=Fill(parent,12,y,584.5f,32,Color.clear);rowWash.raycastTarget=false;rowWash.transform.SetAsFirstSibling();
                inlineKeyRows[label]=rowWash;
                if(row.Action==GameAction.Navigate||row.Action==GameAction.Submit||row.Action==GameAction.Cancel)button.interactable=false;
                shortcutKeys.Add((row.Action,index,label));
            }
        }

        void RefreshInlineKeyRows()
        {
            foreach(var slot in shortcutKeys)
            {
                if(!slot.text||!inlineKeyRows.TryGetValue(slot.text,out var wash))continue;
                bool editing=slot.action==editingShortcut.Action&&slot.index==editingBinding;
                wash.color=Color.clear;
                float bindingWidth=slot.text.transform.parent.GetComponent<RectTransform>().rect.width;
                slot.text.enabled=editingBinding<0||slot.action!=editingShortcut.Action||editing;
                slot.text.rectTransform.anchoredPosition=new Vector2(editing?bindingWidth-160:0,0);
                slot.text.rectTransform.sizeDelta=new Vector2(editing?160:bindingWidth,32);
                slot.text.alignment=editing?TextAnchor.MiddleRight:TextAnchor.MiddleCenter;
                var cap=slot.text.transform.parent.Find("Binding keycap");
                if(cap)
                {
                    cap.gameObject.SetActive(!editing&&slot.text.enabled);
                    float width=Mathf.Clamp(slot.text.preferredWidth+12,34,slot.text.rectTransform.rect.width);
                    var rect=cap.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(width,20.5f);
                    rect.anchoredPosition=new Vector2((slot.text.rectTransform.rect.width-width)/2,-5.5f);
                }
            }
        }
    }
}
