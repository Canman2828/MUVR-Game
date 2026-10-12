using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace ShamrockPlazaTools {
[Serializable] public class Manifest { public MaterialInfo[] materials; public LightInfo[] lights; }
[Serializable] public class MaterialInfo {
 public string source,name,fbx_material,BaseColor,Roughness,Metallic,Alpha,Emission,Normal,MetallicSmoothness;
 public float[] base_color,emission_color; public float roughness,metallic,alpha,normal_strength,emission_strength;
}
[Serializable] public class LightInfo { public string name,type; public float[] position,rotation,color; public float energy; }
public class PlazaImporter:AssetPostprocessor {
 void OnPreprocessModel(){ if(!assetPath.StartsWith("Assets/ShamrockPlaza/Models/"))return; var m=(ModelImporter)assetImporter;m.globalScale=1;m.bakeAxisConversion=true;m.preserveHierarchy=true;m.importCameras=false;m.importLights=false;m.importAnimation=false;m.isReadable=true;m.materialImportMode=ModelImporterMaterialImportMode.ImportStandard; }
 void OnPreprocessTexture(){if(!assetPath.StartsWith("Assets/ShamrockPlaza/Textures/"))return;var t=(TextureImporter)assetImporter;t.maxTextureSize=2048;t.mipmapEnabled=true;t.wrapMode=TextureWrapMode.Repeat;t.textureCompression=TextureImporterCompression.Compressed;
  if(assetPath.Contains("Normal")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}
  else t.sRGBTexture=!(assetPath.Contains("Data")||assetPath.Contains("Roughness")||assetPath.Contains("Metallic")||assetPath.Contains("_Alpha"));
  if(assetPath.Contains("BaseColorAlpha")){t.alphaSource=TextureImporterAlphaSource.FromInput;t.alphaIsTransparency=true;}
 }
}
public static class PlazaBuilder {
 const string R="Assets/ShamrockPlaza";
 static Texture2D Tex(string n)=>string.IsNullOrEmpty(n)?null:AssetDatabase.LoadAssetAtPath<Texture2D>(R+"/Textures/"+n);
 static Color Col(float[] c)=>new Color(c[0],c[1],c[2],c.Length>3?c[3]:1);
 [MenuItem("Shamrock Plaza/Create detailed preview and prefab")]
 public static void Build(){
  var d=JsonUtility.FromJson<Manifest>(File.ReadAllText(R+"/materials.json"));
  Directory.CreateDirectory(R+"/Materials");Directory.CreateDirectory(R+"/Prefabs");Directory.CreateDirectory(R+"/Scenes");Directory.CreateDirectory(R+"/BatchedMeshes");
  bool urp=GraphicsSettings.defaultRenderPipeline && GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("Universal");
  var shader=Shader.Find(urp?"Universal Render Pipeline/Lit":"Standard"); if(!shader)throw new Exception("Supported shader not found");
  var map=new Dictionary<string,Material>();
  foreach(var v in d.materials){
   var path=R+"/Materials/"+v.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}m.shader=shader;
   m.SetColor(urp?"_BaseColor":"_Color",Tex(v.BaseColor)?Color.white:Col(v.base_color));m.SetTexture(urp?"_BaseMap":"_MainTex",Tex(v.BaseColor));
   m.SetFloat("_Metallic",v.metallic);m.SetFloat(urp?"_Smoothness":"_Glossiness",1-v.roughness);
   if(Tex(v.MetallicSmoothness)){m.SetTexture("_MetallicGlossMap",Tex(v.MetallicSmoothness));m.EnableKeyword("_METALLICGLOSSMAP");m.SetFloat("_GlossMapScale",1);if(urp)m.SetFloat("_Smoothness",1);}
   if(Tex(v.Normal)){m.SetTexture("_BumpMap",Tex(v.Normal));m.SetFloat("_BumpScale",v.normal_strength);m.EnableKeyword("_NORMALMAP");}
   if(!string.IsNullOrEmpty(v.Alpha)){
    m.SetFloat("_Cutoff",.4f);m.EnableKeyword("_ALPHATEST_ON");m.SetOverrideTag("RenderType","TransparentCutout");m.renderQueue=(int)RenderQueue.AlphaTest;
    if(urp){m.SetFloat("_AlphaClip",1);m.SetFloat("_Cull",0);}else m.SetFloat("_Mode",1);
   }
   if(v.emission_strength>0&&(v.emission_color[0]+v.emission_color[1]+v.emission_color[2]>.01f||Tex(v.Emission))){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",Tex(v.Emission)?Color.white*v.emission_strength:Col(v.emission_color)*v.emission_strength);m.SetTexture("_EmissionMap",Tex(v.Emission));}
   if(v.source.Contains("_bulb")){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(1f,.48f,.16f)*2.5f);} m.enableInstancing=true;EditorUtility.SetDirty(m);map[v.fbx_material]=m;
  }
  var prev=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(!Application.isBatchMode&&string.IsNullOrEmpty(prev.path)&&prev.isDirty){EditorUtility.DisplayDialog("Save your scene","Save the current scene before creating a separate preview.","OK");return;}
  var mode=Application.isBatchMode||string.IsNullOrEmpty(prev.path)?NewSceneMode.Single:NewSceneMode.Additive;
  var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
  var root=new GameObject("ShamrockPlaza_Detailed");
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(R+"/Models/ShamrockPlaza_Environment.fbx");if(!asset)throw new Exception("FBX missing");
  var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);model.transform.SetParent(root.transform,false);
  foreach(var mr in model.GetComponentsInChildren<MeshRenderer>()){
   var mm=mr.sharedMaterials;for(int i=0;i<mm.Length;i++)if(mm[i]&&map.TryGetValue(mm[i].name,out var mat))mm[i]=mat;mr.sharedMaterials=mm;
   bool grass=mr.name.StartsWith("Grass_Scatter");bool detail=grass||mr.name.StartsWith("Strewn_")||mr.name.StartsWith("Timber_")||mr.name.StartsWith("Ivy_")||mr.name.Contains("Ivy_Wall")||mr.name.StartsWith("pine_sapling")||mr.name.StartsWith("Shamrock_");
   if(grass)mr.shadowCastingMode=ShadowCastingMode.Off;
   if(!detail){var mf=mr.GetComponent<MeshFilter>();if(mf&&mf.sharedMesh){var c=mr.gameObject.AddComponent<MeshCollider>();c.sharedMesh=mf.sharedMesh;}}
  }
  BatchScatter(model,"Grass_Scatter");BatchScatter(model,"Strewn_Masonry");
  var lighting=new GameObject("Atmosphere_Lighting");lighting.transform.SetParent(root.transform,false);
  foreach(var v in d.lights){var g=new GameObject(v.name);g.transform.SetParent(lighting.transform,false);g.transform.position=new Vector3(v.position[0],v.position[2],v.position[1]);var l=g.AddComponent<Light>();l.color=Col(v.color);
   if(v.type=="SUN"){l.type=LightType.Directional;l.intensity=.4f;l.shadows=LightShadows.Soft;l.shadowBias=.12f;l.shadowNormalBias=.5f;g.transform.rotation=Quaternion.Euler(42,-35,0);}
   else {l.type=LightType.Point;l.intensity=4f;l.range=7;l.shadows=LightShadows.None;}
  }
  PrefabUtility.SaveAsPrefabAsset(root,R+"/Prefabs/ShamrockPlaza_Detailed.prefab");
  RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.06f,.08f,.12f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.065f,.09f,.13f);RenderSettings.fogDensity=.013f;
  var cam=new GameObject("Preview Camera").AddComponent<Camera>();cam.transform.position=new Vector3(-1.83f,2.2f,1.1f);cam.transform.LookAt(new Vector3(-1.83f,1.2f,10));cam.fieldOfView=73;cam.nearClipPlane=.08f;cam.backgroundColor=RenderSettings.fogColor;cam.clearFlags=CameraClearFlags.SolidColor;
  EditorSceneManager.SaveScene(scene,R+"/Scenes/ShamrockPlaza_Detailed.unity");AssetDatabase.SaveAssets();AssetDatabase.Refresh();
  int renderers=model.GetComponentsInChildren<MeshRenderer>().Count(m=>m.enabled);
  if(model.GetComponentsInChildren<MeshRenderer>().Any(m=>m.sharedMaterials.Any(mat=>!mat||!mat.shader||mat.shader.name.Contains("InternalError"))))throw new Exception("Missing material/shader");
  if(model.GetComponentsInChildren<Transform>().Any(t=>t.name.StartsWith("PH_")||t.name.StartsWith("UCX_")))throw new Exception("Reference blockout exported");
  File.WriteAllText(Path.GetFullPath("../Work/unity_validation.json"),"{\"status\":\"PASS\",\"active_mesh_renderers\":"+renderers+",\"reference_blockout_exported\":false}");
  if(Application.isBatchMode){AssetDatabase.ExportPackage(R,Path.GetFullPath("../ShamrockPlaza_UnityReady.unitypackage"),ExportPackageOptions.Recurse);var rt=new RenderTexture(1400,950,24);rt.antiAliasing=4;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var image=new Texture2D(1400,950,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,950),0,0);image.Apply();File.WriteAllBytes(Path.GetFullPath("../Previews/Unity_Imported_Preview.png"),image.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
  if(prev.IsValid())UnityEngine.SceneManagement.SceneManager.SetActiveScene(prev);
  Debug.Log("SHAMROCK_UNITY_READY "+renderers+" renderers");
 }
 static void BatchScatter(GameObject root,string prefix){
  var items=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith(prefix)).ToArray();
  var groups=items.GroupBy(r=>Mathf.FloorToInt(r.bounds.center.x/6)+"_"+Mathf.FloorToInt(r.bounds.center.z/6)+"_"+r.sharedMaterial.name);
  int n=0;foreach(var group in groups){
   var mesh=new Mesh();mesh.indexFormat=IndexFormat.UInt32;mesh.name=prefix+"_Cell_"+n;
   var combines=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=r.transform.localToWorldMatrix}).ToArray();mesh.CombineMeshes(combines,true,true);mesh.RecalculateBounds();
   string path=R+"/BatchedMeshes/"+prefix+"_"+(n++)+".asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);
   var g=new GameObject(mesh.name);g.transform.SetParent(root.transform,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=group.First().sharedMaterial;if(prefix.Contains("Grass"))renderer.shadowCastingMode=ShadowCastingMode.Off;
   foreach(var r in group)r.enabled=false;
  }
 }
}
}

