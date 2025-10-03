using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(SkinnedMeshRenderer))]
public class BakeAndPlaceMesh : MonoBehaviour
{
    [ContextMenu("Bake and Save")]
    void BakeAndSave()
    {
        SkinnedMeshRenderer smr = GetComponent<SkinnedMeshRenderer>();
        if (smr == null)
        {
            Debug.LogError("No SkinnedMeshRenderer");
            return;
        }

        Mesh bakedMesh = new Mesh();
        smr.BakeMesh(bakedMesh);

        Matrix4x4 matrix = smr.transform.localToWorldMatrix;
        Vector3[] vertices = bakedMesh.vertices;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = matrix.MultiplyPoint3x4(vertices[i]);
        }

        bakedMesh.vertices = vertices;
        bakedMesh.RecalculateBounds();

        GameObject bakedGO = new GameObject("CorrectBakedGrabCollider");
        MeshFilter mf = bakedGO.AddComponent<MeshFilter>();
        MeshRenderer mr = bakedGO.AddComponent<MeshRenderer>();

        mf.sharedMesh = bakedMesh;
        mr.sharedMaterial = smr.sharedMaterial;

        bakedGO.transform.position = Vector3.zero;
        bakedGO.transform.rotation = Quaternion.identity;
        bakedGO.transform.localScale = Vector3.one;

#if UNITY_EDITOR
        string savePath = "Assets/CorrectBakedGrabCollider.asset";
        AssetDatabase.CreateAsset(bakedMesh, savePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Bake Finish: {savePath}");
#endif
    }
}
