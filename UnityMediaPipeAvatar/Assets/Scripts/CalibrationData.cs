using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
/// <summary>
/// Cache various values which will be reused during the runtime.
/// </summary>
public class CalibrationData
{
    [SerializeField] public string parentn, childn, tparentn, tchildn;
    [System.NonSerialized] public Transform parent, child, tparent, tchild;
    [SerializeField] public Vector3 initialDir;
    [SerializeField] public Quaternion initialRotation;
    [SerializeField] public Quaternion targetRotation;

    public void Tick(Quaternion newTarget, float speed)
    {
        parent.rotation = newTarget;
        parent.rotation = Quaternion.Lerp(parent.rotation, targetRotation, Time.deltaTime * speed);
    }

    public Vector3 CurrentDirection => (tchild.position - tparent.position).normalized;

    public CalibrationData(Transform topParent, Transform fparent, Transform fchild,
        Transform tparent, Transform tchild)
    {
        initialDir = (tchild.position - tparent.position).normalized;
        initialRotation = fparent.rotation;
        this.parent = fparent;
        this.child = fchild;
        this.tparent = tparent;
        this.tchild = tchild;
        parentn = GetPath(parent);
        childn = GetPath(child);
        tparentn = GetPath(tparent);
        tchildn = GetPath(tchild);
    }

    public CalibrationData ReconstructReferences()
    {
        SetFromPath(parentn, out parent);
        SetFromPath(childn, out child);
        SetFromPath(tparentn, out tparent);
        SetFromPath(tchildn, out tchild);
        return this;
    }

    /// <summary>
    /// Finds a Transform by its full hierarchy path using recursive search.
    /// Supports paths like "Root/Parent/Child/".
    /// </summary>
    private void SetFromPath(string path, out Transform target)
    {
        target = null;
        if (string.IsNullOrEmpty(path)) return;

        // Remove trailing slash
        path = path.TrimEnd('/');

        string[] parts = path.Split('/');
        if (parts.Length == 0) return;

        // Find root object
        GameObject root = GameObject.Find(parts[0]);
        if (root == null)
        {
            Debug.LogWarning($"CalibrationData: Could not find root '{parts[0]}'");
            return;
        }

        if (parts.Length == 1)
        {
            target = root.transform;
            return;
        }

        // Walk down the hierarchy
        Transform current = root.transform;
        for (int i = 1; i < parts.Length; i++)
        {
            Transform found = FindChildRecursive(current, parts[i]);
            if (found == null)
            {
                Debug.LogWarning($"CalibrationData: Could not find '{parts[i]}' under '{current.name}'");
                return;
            }
            current = found;
        }
        target = current;
    }

    /// <summary>
    /// Recursively searches for a child by name.
    /// </summary>
    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private string GetPath(Transform t)
    {
        List<Transform> chain = new List<Transform>();
        Transform current = t;
        while (current != null)
        {
            chain.Add(current);
            current = current.parent;
        }
        chain.Reverse();
        string s = "";
        foreach (Transform node in chain)
            s += node.name + "/";
        return s;
    }
}