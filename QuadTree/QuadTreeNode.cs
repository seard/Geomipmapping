using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The quad index enum which simplifies node links
/// </summary>
public enum QuadTreeIndex
{
    TopLeft = 0,        // 00
    TopRight = 1,       // 01
    BottomLeft = 2,     // 10
    BottomRight = 3,    // 11
}

public class QuadTreeNode<T>
{
    public Vector3 Position;
    public float Size;
    public int Depth;
    public QuadTreeNode<T>[] SubNodes;
    public QuadTreeNode<T> Parent;
    public T Data;

    public QuadTreeNode(QuadTreeNode<T> parent, Vector3 position, float size, int depth, T data = default(T))
    {
        Position = position;
        Size = size;
        Depth = depth;
        Parent = parent;
        Data = data;
    }

    public void Subdivide()
    {
        SubNodes = new QuadTreeNode<T>[4];

        for (int i = 0; i < SubNodes.Length; i++)
        {
            Vector3 newPos = Position;

            if ((i & 2) == 2)
                newPos.z -= Size * 0.25f;
            else
                newPos.z += Size * 0.25f;

            if ((i & 1) == 1)
                newPos.x += Size * 0.25f;
            else
                newPos.x -= Size * 0.25f;

            SubNodes[i] = new QuadTreeNode<T>(this, newPos, Size * 0.5f, Depth + 1);
        }
    }

    public bool IsLeaf
    {
        get { return SubNodes == null; }
    }

    private static int GetIndexOfPosition(Vector3 lookupPosition, Vector3 nodePosition)
    {
        int index = 0;

        index |= lookupPosition.z < nodePosition.z ? 2 : 0;
        index |= lookupPosition.x > nodePosition.x ? 1 : 0;

        return index;
    }
}