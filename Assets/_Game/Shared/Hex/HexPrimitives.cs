using System;
using UnityEngine;

namespace Ggj2026.Game.Shared.Hex
{
    [Serializable]
    public struct HexCoord : IEquatable<HexCoord>
    {
        public static readonly HexCoord Zero = new HexCoord(0, 0);

        public int Q;
        public int R;
        public int S => -Q - R;

        public HexCoord(int q, int r)
        {
            Q = q;
            R = r;
        }

        public HexCoord Neighbor(int direction)
        {
            HexCoord offset = direction switch
            {
                0 => new HexCoord(1, 0),
                1 => new HexCoord(1, -1),
                2 => new HexCoord(0, -1),
                3 => new HexCoord(-1, 0),
                4 => new HexCoord(-1, 1),
                _ => new HexCoord(0, 1)
            };

            return this + offset;
        }

        public int DistanceTo(HexCoord other)
        {
            return (Math.Abs(Q - other.Q) + Math.Abs(R - other.R) + Math.Abs(S - other.S)) / 2;
        }

        public Vector3 ToWorld(float radius)
        {
            float x = radius * Mathf.Sqrt(3f) * (Q + (R * 0.5f));
            float z = radius * 1.5f * R;
            return new Vector3(x, 0f, z);
        }

        public static HexCoord FromWorld(Vector3 worldPosition, float radius)
        {
            float q = ((Mathf.Sqrt(3f) / 3f) * worldPosition.x - (1f / 3f) * worldPosition.z) / radius;
            float r = ((2f / 3f) * worldPosition.z) / radius;
            return Round(q, r);
        }

        public static HexCoord Round(float q, float r)
        {
            float s = -q - r;

            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float qDiff = Mathf.Abs(rq - q);
            float rDiff = Mathf.Abs(rr - r);
            float sDiff = Mathf.Abs(rs - s);

            if (qDiff > rDiff && qDiff > sDiff)
            {
                rq = -rr - rs;
            }
            else if (rDiff > sDiff)
            {
                rr = -rq - rs;
            }

            return new HexCoord(rq, rr);
        }

        public bool Equals(HexCoord other) => Q == other.Q && R == other.R;
        public override bool Equals(object obj) => obj is HexCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Q, R);
        public override string ToString() => $"({Q},{R},{S})";
        public static HexCoord operator +(HexCoord left, HexCoord right) => new HexCoord(left.Q + right.Q, left.R + right.R);
        public static HexCoord operator -(HexCoord left, HexCoord right) => new HexCoord(left.Q - right.Q, left.R - right.R);
        public static bool operator ==(HexCoord left, HexCoord right) => left.Equals(right);
        public static bool operator !=(HexCoord left, HexCoord right) => !left.Equals(right);

        public static HexCoord[] GetNeighbors(HexCoord origin)
        {
            return new[]
            {
                origin.Neighbor(0),
                origin.Neighbor(1),
                origin.Neighbor(2),
                origin.Neighbor(3),
                origin.Neighbor(4),
                origin.Neighbor(5)
            };
        }
    }

    [Serializable]
    public struct HexEdgeCoord : IEquatable<HexEdgeCoord>
    {
        public HexCoord A;
        public HexCoord B;

        public HexEdgeCoord(HexCoord a, HexCoord b)
        {
            if (Compare(a, b) <= 0)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public static HexEdgeCoord FromCellAndDirection(HexCoord cell, int direction)
        {
            return new HexEdgeCoord(cell, cell.Neighbor(direction));
        }

        public bool Contains(HexCoord cell)
        {
            return A == cell || B == cell;
        }

        public HexCoord GetOther(HexCoord cell)
        {
            return A == cell ? B : A;
        }

        public Vector3 Midpoint(float radius)
        {
            return (A.ToWorld(radius) + B.ToWorld(radius)) * 0.5f;
        }

        public bool Equals(HexEdgeCoord other) => A == other.A && B == other.B;
        public override bool Equals(object obj) => obj is HexEdgeCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(A, B);
        public override string ToString() => $"Edge[{A}<->{B}]";
        public static bool operator ==(HexEdgeCoord left, HexEdgeCoord right) => left.Equals(right);
        public static bool operator !=(HexEdgeCoord left, HexEdgeCoord right) => !left.Equals(right);

        private static int Compare(HexCoord left, HexCoord right)
        {
            int qCompare = left.Q.CompareTo(right.Q);
            if (qCompare != 0)
            {
                return qCompare;
            }

            return left.R.CompareTo(right.R);
        }
    }

    [Serializable]
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int X;
        public int Y;

        public ChunkCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static ChunkCoord FromHex(HexCoord coord, int chunkSize)
        {
            return new ChunkCoord(FloorDiv(coord.Q, chunkSize), FloorDiv(coord.R, chunkSize));
        }

        public bool Equals(ChunkCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"Chunk({X},{Y})";
        public static bool operator ==(ChunkCoord left, ChunkCoord right) => left.Equals(right);
        public static bool operator !=(ChunkCoord left, ChunkCoord right) => !left.Equals(right);

        private static int FloorDiv(int value, int divisor)
        {
            int result = value / divisor;
            int remainder = value % divisor;
            if (remainder != 0 && ((remainder < 0) ^ (divisor < 0)))
            {
                result--;
            }

            return result;
        }
    }

    public static class HexMetrics
    {
        public const int DefaultChunkSize = 24;
        public const int DefaultWorldSize = 384;
        public const float DefaultHexRadius = 1f;
    }
}
