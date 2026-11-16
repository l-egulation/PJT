using System;
using UnityEngine;
using UnityEditor;
namespace RuralNature32.Editor
{
    [Serializable] public class ForestPaletteGrade
    {public float saturation=1,contrast=1;public float[] lift=new float[3];}
    public static class ForestPaletteMaterials
    {
        public static Material Get(string name,ForestPaletteGrade grade)
        {
            if(grade==null)return NatureDemo.Unlit;
            var shader=Shader.Find("RuralNature32/Soft Palette");
            if(!shader)throw new InvalidOperationException("Forest palette shader not found");
            if(ShaderUtil.ShaderHasError(shader)){foreach(var message in ShaderUtil.GetShaderMessages(shader))Debug.LogError(message.message+" at "+message.file+":"+message.line);throw new InvalidOperationException("Forest palette shader did not compile");}
            string path=NatureBaker.Root+"/Data/SoftPalette_"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}else material.shader=shader;
            material.SetFloat("_Saturation",grade.saturation);material.SetFloat("_Contrast",grade.contrast);
            material.SetVector("_Lift",new Vector4(grade.lift[0],grade.lift[1],grade.lift[2],0));EditorUtility.SetDirty(material);return material;
        }
    }
}
