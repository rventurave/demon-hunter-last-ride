using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Reins;
using JapaneseDemonHunter.Monsters;
using JapaneseDemonHunter.Gameplay;
using Object=UnityEngine.Object;
public static class InspectNextRound
{
    public static object DebugPlacementState()
    {
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var entry=spawner.SpawnEntries.First(e=>e.movementType==MonsterMovementType.Ground);
        var left=new MonsterSpawnEntry {prefab=entry.prefab,movementType=MonsterMovementType.Ground,spawnDirection=MonsterSpawnDirection.LeftForest};
        var right=new MonsterSpawnEntry {prefab=entry.prefab,movementType=MonsterMovementType.Ground,spawnDirection=MonsterSpawnDirection.RightForest};
        bool canLeft=spawner.TryFindSpawnPosition(left,out var lp),canRight=spawner.TryFindSpawnPosition(right,out var rp);
        return new {active=spawner.ActiveMonsterCount,maximum=spawner.MaximumActiveMonsters,stopped=spawner.SpawningStopped,
            timeScale=Time.timeScale,phase=Object.FindAnyObjectByType<GameSessionController>().Phase.ToString(),canLeft,canRight,
            left=lp.ToString(),right=rp.ToString(),groundSurfaces=Object.FindObjectsByType<MonsterGroundSurface>(FindObjectsInactive.Exclude).Length,
            treeNames=string.Join(";",Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Where(t=>t.name.StartsWith("Tree_")).Take(3).Select(t=>t.name+":"+t.position.ToString()))};
    }
    public static object ShaderErrors()
    {
        var shader=Shader.Find("JapaneseDemonHunter/VictorySpark");
        return new {shader=shader.name,messages=string.Join(";",ShaderUtil.GetShaderMessages(shader).Select(m=>m.severity+" "+m.message+" line="+m.line))};
    }
    public static object FortressDoor()
    {
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Fortress.fbx"));
        model.transform.position=Vector3.zero;
        var lines=new System.Collections.Generic.List<string>();
        try
        {
            var mesh=model.GetComponentInChildren<MeshFilter>();
            var collider=mesh.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh=mesh.sharedMesh;
            Physics.SyncTransforms();
            foreach(float y in new[]{.08f,.1f,.12f,.15f,.18f,.21f,.24f,.27f})
            {
                var xs=new System.Collections.Generic.List<float>();
                for(float x=-.3f;x<=.3f;x+=.005f)
                    if(!collider.Raycast(new Ray(new Vector3(x,y,1.2f),Vector3.back),out var hit,.8f)) xs.Add(x);
                lines.Add(y+":"+string.Join(",",xs.Select(x=>x.ToString("F3"))));
            }
            return new {front="+Z",openSamples=string.Join(";",lines),meshTransform=mesh.transform.localToWorldMatrix.ToString()};
        }
        finally {Object.DestroyImmediate(model);}
    }
    public static object FortressViews()
    {
        var preview=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Fortress.fbx"));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(model,preview);
        var cameraObject=new GameObject("Fortress diagnostic camera");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,preview);
        var camera=cameraObject.AddComponent<Camera>(); camera.scene=preview;
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.16f,.2f,.27f);
        camera.orthographic=true; camera.orthographicSize=.85f;
        var lightObject=new GameObject("Diagnostic light"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject,preview);
        lightObject.AddComponent<Light>().type=LightType.Directional; lightObject.transform.rotation=Quaternion.Euler(35,-30,0);
        var rt=new RenderTexture(960,720,24); var pixels=new Texture2D(960,720,TextureFormat.RGB24,false);
        var old=RenderTexture.active;
        try
        {
            camera.targetTexture=rt;
            for(int side=0;side<4;side++)
            {
                camera.transform.position=new[]{new Vector3(0,.75f,-3),new Vector3(0,.75f,3),new Vector3(-3,.75f,0),new Vector3(3,.75f,0)}[side];
                camera.transform.LookAt(new Vector3(0,.4f,0)); camera.Render(); RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,960,720),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes("Library/FortressView"+side+".png",pixels.EncodeToPNG());
            }
            return new {files="Library/FortressView0.png .. FortressView3.png"};
        }
        finally {RenderTexture.active=old; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels); UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);}
    }
    public static object Architecture()
    {
        var spawner=Object.FindAnyObjectByType<MonsterSpawner>();
        var road=Object.FindAnyObjectByType<ForestRoad>();
        var prefab=spawner.SpawnEntries.First(e=>e.movementType==MonsterMovementType.Ground).prefab;
        var animator=prefab.GetComponentInChildren<Animator>();
        var controller=animator.runtimeAnimatorController as AnimatorController;
        var fortress=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Fortress.fbx");
        return new {
            levelDistance=road.LevelDistance,road=EditorJsonUtility.ToJson(road),spawner=EditorJsonUtility.ToJson(spawner),
            animatorPath=AssetDatabase.GetAssetPath(controller),states=string.Join("; ",controller.layers.SelectMany(l=>l.stateMachine.states).Select(s=>s.state.name+" clip="+s.state.motion?.name)),
            clips=string.Join("; ",AssetDatabase.FindAssets("t:Model",new[]{"Assets/Art/Zombie"}).Select(AssetDatabase.GUIDToAssetPath).SelectMany(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Select(c=>p+" : "+c.name))),
            fortressMeshes=string.Join("; ",fortress.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.name+" "+m.sharedMesh.bounds.ToString()+" vertices="+m.sharedMesh.vertexCount)),
            roots=string.Join("; ",UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Select(g=>g.name+" "+g.transform.position.ToString())),
            trees=string.Join("; ",Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t=>t.name.StartsWith("Tree_")).Take(12).Select(t=>t.name+" "+t.position.ToString()+" colliders="+t.GetComponentsInChildren<Collider>(true).Length))
        };
    }
}



