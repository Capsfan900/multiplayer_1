using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AssetImportValidation
{
    const string FixturePath = "Assets/Wildstone/Art/AxisFixture.fbx";

    [MenuItem("Wildstone/Validate Asset Axis Fixture")]
    public static void ValidateFixture()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(FixturePath);
        Require(root != null, "Axis fixture FBX did not import");
        var importer = AssetImporter.GetAtPath(FixturePath) as ModelImporter;
        Require(importer != null, "Axis fixture has no ModelImporter");
        var grip = Find(root.transform, "Grip");
        var muzzle = Find(root.transform, "Muzzle");
        var bar = Find(root.transform, "OneMetreBar");
        var tab = Find(root.transform, "PositiveZTab");
        Require(grip && muzzle && bar && tab, "Missing named fixture geometry or anchors");

        // The imported root carries the FBX axis and handedness conversion.
        CheckPoint(grip.position, new Vector3(.12f, .10f, -.05f), "Grip");
        CheckPoint(muzzle.position, new Vector3(.95f, .10f, .05f), "Muzzle");

        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        var materials = new HashSet<string>();
        int triangles = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = filter.sharedMesh;
            Require(mesh != null, "Missing fixture mesh");
            var toAsset = filter.transform.localToWorldMatrix;
            foreach (var vertex in mesh.vertices)
            {
                var point = toAsset.MultiplyPoint3x4(vertex);
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
            triangles += mesh.triangles.Length / 3;
            Require(mesh.normals.Length == mesh.vertexCount, "Mesh normals missing");
            foreach (var normal in mesh.normals)
                Require(normal.sqrMagnitude > .9f, "Degenerate fixture normal");
            var renderer = filter.GetComponent<MeshRenderer>();
            Require(renderer != null, "Fixture renderer missing");
            foreach (var material in renderer.sharedMaterials)
                if (material != null) materials.Add(material.name);
        }
        CheckPoint(min, new Vector3(0, 0, -.05f), "bounds min");
        CheckPoint(max, new Vector3(1, .30f, .16f), "bounds max");
        CheckPoint(Center(tab), new Vector3(.82f, .25f, .10f), "positive Z tab");
        Require(triangles == 24, "Expected two outward-faced cubes (24 triangles), got " + triangles);
        Require(materials.Count == 2, "Expected two distinct imported materials, got " + materials.Count);
        Debug.Log("Wildstone axis fixture PASS: 1 m +X, correct +Z tab, anchors, normals, materials and bounds; importer scale=" + importer.globalScale);
    }

    static Transform Find(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name) return child;
        return null;
    }

    static Vector3 Center(Transform node)
    {
        var filter = node.GetComponent<MeshFilter>();
        Require(filter != null && filter.sharedMesh != null, "Tab mesh missing");
        return node.TransformPoint(filter.sharedMesh.bounds.center);
    }

    static void CheckPoint(Vector3 actual, Vector3 expected, string label)
    {
        Require(Vector3.Distance(actual, expected) <= .005f, label + ": " + actual + " expected " + expected);
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Wildstone asset import: " + message);
    }
}

public sealed class WildstoneModelImportPostprocessor : AssetPostprocessor
{
    static readonly HashSet<string> AuthoredModels = new HashSet<string>(StringComparer.Ordinal)
    {
        "AxisFixture.fbx", "LineDriver.fbx", "PulseLobber.fbx", "ReboundDisc.fbx", "PushLens.fbx",
        "Grenade.fbx", "StunGun.fbx", "LaserSword.fbx", "LaserPistol.fbx", "LaserShotgun.fbx", "LaserSniper.fbx", "LaserSMG.fbx", "FirstPersonHands.fbx", "BlinkPickup.fbx",
        "ShieldPickup.fbx", "HealthPickup.fbx", "WildstoneHuman.fbx", "WildstoneHumanTeam02.fbx", "WildstoneHumanTeam03.fbx", "WildstoneHumanTeam04.fbx"
    };

    void OnPostprocessModel(GameObject model)
    {
        const string directory = "Assets/Wildstone/Art/";
        if (!assetPath.StartsWith(directory, StringComparison.Ordinal) ||
            !AuthoredModels.Contains(assetPath.Substring(directory.Length))) return;
        // Authored (X,-Z,Y) Blender exports need an X reflection on Unity import.
        model.transform.localScale = Vector3.Scale(model.transform.localScale, new Vector3(-1, 1, 1));
    }
}
