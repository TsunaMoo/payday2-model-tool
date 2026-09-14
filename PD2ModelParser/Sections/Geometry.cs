using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.RightsManagement;
using static PD2ModelParser.Sections.Geometry;

namespace PD2ModelParser.Sections
{
    public class GeometryWeightGroups
    {
        public readonly ushort Bones1;
        public readonly ushort Bones2;
        public readonly ushort Bones3;
        public readonly ushort Bones4;

        public GeometryWeightGroups(ushort b1, ushort b2, ushort b3, ushort b4)
        {
            Bones1 = b1;
            Bones2 = b2;
            Bones3 = b3;
            Bones4 = b4;
        }

        public GeometryWeightGroups(BinaryReader instream)
        {
            Bones1 = instream.ReadUInt16();
            Bones2 = instream.ReadUInt16();
            Bones3 = instream.ReadUInt16();
            Bones4 = instream.ReadUInt16();
        }

        public void StreamWrite(BinaryWriter outstream)
        {
            outstream.Write(Bones1);
            outstream.Write(Bones2);
            outstream.Write(Bones3);
            outstream.Write(Bones4);
        }

        public override string ToString()
        {
            return "{ Bones1=" + Bones1 +
                   ", Bones2=" + Bones2 +
                   ", Bones3=" + Bones3 +
                   ", Bones4=" + Bones4 + " }";
        }
    }

    public struct GeometryColor
    {
        public readonly byte red;
        public readonly byte green;
        public readonly byte blue;
        public readonly byte alpha;

        public GeometryColor(byte red, byte green, byte blue, byte alpha)
        {
            this.red = red;
            this.green = green;
            this.blue = blue;
            this.alpha = alpha;
        }

        public GeometryColor(BinaryReader instream)
        {
            blue = instream.ReadByte();
            green = instream.ReadByte();
            red = instream.ReadByte();
            alpha = instream.ReadByte();
        }

        public void StreamWrite(BinaryWriter outstream)
        {
            outstream.Write(blue);
            outstream.Write(green);
            outstream.Write(red);
            outstream.Write(alpha);
        }

        public override string ToString()
        {
            return "{Red=" + red +
                   ", Green=" + green +
                   ", Blue=" + blue +
                   ", Alpha=" + alpha + "}";
        }
    }

    public class GeometryHeader
    {
        public static readonly IReadOnlyList<uint> ItemSizes =
            new List<uint> { 0, 4, 8, 12, 16, 4, 4, 8, 4, 4 };

        public UInt32 ItemSize { get; set; }
        public GeometryChannelTypes ItemType { get; set; }

        public GeometryHeader()
        {
        }

        public GeometryHeader(UInt32 size, GeometryChannelTypes type)
        {
            ItemSize = size;
            ItemType = type;
        }

        public uint ItemSizeBytes
        {
            get
            {
                return ItemSizes[(int)ItemSize];
            }
        }
    }

    public enum GeometryChannelTypes
    {
        POSITION0 = 1,
        NORMAL0 = 2,
        POSITION1 = 3,
        NORMAL1 = 4,
        COLOR0 = 5,
        COLOR1 = 6,
        TEXCOORD0 = 7,
        TEXCOORD1 = 8,
        TEXCOORD2 = 9,
        TEXCOORD3 = 10,
        TEXCOORD4 = 11,
        TEXCOORD5 = 12,
        TEXCOORD6 = 13,
        TEXCOORD7 = 14,
        TEXCOORD8 = 15,
        TEXCOORD9 = 16,
        BLENDINDICES0 = 17,
        BLENDINDICES1 = 18,
        BLENDWEIGHT0 = 19,
        BLENDWEIGHT1 = 20,
        POINTSIZE0 = 21,
        BINORMAL0 = 22,
        TANGENT0 = 23,
        BINORMAL1 = 24,
        TANGENT1 = 25,
    }

    public struct RaidLegacyBlendData
    {
        public ushort Value0;
        public ushort Value1;
        public uint Reserved;

        public RaidLegacyBlendData(
            ushort value0,
            ushort value1,
            uint reserved)
        {
            Value0 = value0;
            Value1 = value1;
            Reserved = reserved;
        }

        public uint Packed
        {
            get
            {
                return (uint)Value0 | ((uint)Value1 << 16);
            }
        }

        public override string ToString()
        {
            return "{ Value0=" + Value0 +
                   ", Value1=" + Value1 +
                   ", Reserved=0x" + Reserved.ToString("X8") +
                   " }";
        }
    }

    [ModelFileSection(Tags.geometry_tag)]
    class Geometry : AbstractSection, ISection, IHashNamed
    {
        public uint vert_count;
        public List<Vector2>[] UVs = new List<Vector2>[8];
        public List<Vector2> uv0 => UVs[0];
        public List<Vector2> uv1 => UVs[1];
        public List<GeometryHeader> Headers { get; private set; } = new List<GeometryHeader>();
        public List<Vector3> verts = new List<Vector3>();
        public List<Vector3> position1 = new List<Vector3>();
        public List<Vector3> normals = new List<Vector3>();
        public List<Vector3> normal1 = new List<Vector3>();
        public List<GeometryColor> vertex_colors = new List<GeometryColor>();
        public List<GeometryColor> vertex_colors1 = new List<GeometryColor>();
        public List<GeometryWeightGroups> weight_groups = new List<GeometryWeightGroups>();
        public List<GeometryWeightGroups> weight_groups1 = new List<GeometryWeightGroups>();
        public List<Vector3> weights = new List<Vector3>();
        public List<Vector4> weights1 = new List<Vector4>();
        public List<Vector3> binormals = new List<Vector3>();
        public List<Vector3> tangents = new List<Vector3>();
        public List<RaidLegacyBlendData> raidLegacyBlendData = new List<RaidLegacyBlendData>();
        public List<float> point_sizes = new List<float>();
        public enum GeometryFormat { Payday , Raid , RaidLegacy }
        public GeometryFormat Format { get; private set; }
        private static readonly uint[] PaydayItemSizes = { 0, 4, 8, 12, 16, 4, 4, 8, 4, 4 };
        private static readonly uint[] RaidItemSizes = { 0, 4, 8, 12, 16, 4, 4, 8, 12, 8 };
        public HashName HashName { get; set; }
        public byte[] remaining_data = null;

        private static float UnpackSignedByte(byte value)
        {
            return (value / 255.0f) * 2.0f - 1.0f;
        }

        private static byte PackSignedByte(float value)
        {
            value = Math.Clamp(value, -1.0f, 1.0f);
            return (byte)((value + 1.0f) * 127.5f);
        }

        private static Vector3 ReadPackedVector3(BinaryReader instream)
        {
            byte z = instream.ReadByte();
            byte y = instream.ReadByte();
            byte x = instream.ReadByte();

            instream.ReadByte();

            return new Vector3(
                UnpackSignedByte(x),
                UnpackSignedByte(y),
                UnpackSignedByte(z));
        }

        private static void WritePackedVector3(
            BinaryWriter outstream,
            Vector3 value)
        {
            outstream.Write(PackSignedByte(value.Z));
            outstream.Write(PackSignedByte(value.Y));
            outstream.Write(PackSignedByte(value.X));
            outstream.Write((byte)0);
        }

        private static Vector3 ReadFloatVector3(BinaryReader instream)
        {
            return new Vector3(
                instream.ReadSingle(),
                instream.ReadSingle(),
                instream.ReadSingle());
        }

        private static void WriteFloatVector3(
            BinaryWriter outstream,
            Vector3 value)
        {
            outstream.Write(value.X);
            outstream.Write(value.Y);
            outstream.Write(value.Z);
        }

        private Vector3 ReadVector3ByType(
            BinaryReader instream,
            uint type)
        {
            if (type == 3)
                return ReadFloatVector3(instream);

            if (type == 8)
            {
                if (Format == GeometryFormat.Raid)
                    return ReadFloatVector3(instream);

                return ReadPackedVector3(instream);
            }

            throw new Exception(
                $"Unsupported Vector3 geometry type {type}");
        }

        private void WriteVector3ByType(
            BinaryWriter outstream,
            Vector3 value,
            uint type)
        {
            if (type == 3)
            {
                WriteFloatVector3(outstream, value);
                return;
            }

            if (type == 8)
            {
                if (Format == GeometryFormat.Raid)
                    WriteFloatVector3(outstream, value);
                else
                    WritePackedVector3(outstream, value);

                return;
            }

            throw new Exception(
                $"Unsupported Vector3 geometry type {type}");
        }

        private static float ReadHalf(BinaryReader instream)
        {
            ushort raw = instream.ReadUInt16();
            return (float)BitConverter.UInt16BitsToHalf(raw);
        }

        private static void WriteHalf(
            BinaryWriter outstream,
            float value)
        {
            ushort raw =
                BitConverter.HalfToUInt16Bits((Half)value);

            outstream.Write(raw);
        }

        private uint GetItemSizeBytes(GeometryHeader head)
        {
            uint[] sizes =
                Format == GeometryFormat.Raid
                    ? RaidItemSizes
                    : PaydayItemSizes;

            return sizes[(int)head.ItemSize];
        }

        public Geometry Clone()
        {
            var src = this;
            var dst = new Geometry();

            dst.vert_count = vert_count;

            dst.Headers.AddRange(
                src.Headers.Select(
                    i => new GeometryHeader(
                        i.ItemSize,
                        i.ItemType)));

            dst.verts.AddRange(src.verts);
            dst.uv0.AddRange(src.uv0);
            dst.uv1.AddRange(src.uv1);
            dst.normals.AddRange(src.normals);
            dst.vertex_colors.AddRange(src.vertex_colors);
            dst.weight_groups.AddRange(src.weight_groups);
            dst.weights.AddRange(src.weights);
            dst.binormals.AddRange(src.binormals);
            dst.tangents.AddRange(src.tangents);
            dst.raidLegacyBlendData.AddRange(
                src.raidLegacyBlendData);

            dst.HashName = src.HashName;

            return dst;
        }

        public Geometry()
        {
            SectionId = 0;

            for (int i = 0; i < UVs.Length; i++)
                UVs[i] = new List<Vector2>();
        }

        public Geometry(obj_data newobject) : this()
        {
            vert_count = (uint)newobject.verts.Count;

            Headers.Add(
                new GeometryHeader(
                    3,
                    GeometryChannelTypes.POSITION0));

            Headers.Add(
                new GeometryHeader(
                    9,
                    GeometryChannelTypes.TEXCOORD0));

            Headers.Add(
                new GeometryHeader(
                    8,
                    GeometryChannelTypes.NORMAL0));

            Headers.Add(
                new GeometryHeader(
                    8,
                    GeometryChannelTypes.BINORMAL0));

            Headers.Add(
                new GeometryHeader(
                    8,
                    GeometryChannelTypes.TANGENT0));

            verts = newobject.verts;
            UVs[0] = newobject.uv;
            normals = newobject.normals;

            HashName =
                new HashName(
                    newobject.object_name + ".Geometry");
        }

        public Geometry(
            BinaryReader instream,
            SectionHeader section) : this()
        {
            SectionId = section.id;

            vert_count = instream.ReadUInt32();

            uint header_count =
                instream.ReadUInt32();

            uint calc_size = 0;

            for (int x = 0; x < header_count; x++)
            {
                GeometryHeader header =
                    new GeometryHeader();

                header.ItemSize =
                    instream.ReadUInt32();

                uint itemType =
                    instream.ReadUInt32();

                if (section.legacy &&
                    itemType >
                    (uint)GeometryChannelTypes.TEXCOORD7)
                {
                    itemType += 2;
                }

                header.ItemType =
                    (GeometryChannelTypes)itemType;

                Headers.Add(header);
            }

            Format =
                section.legacy
                    ? Headers.Any(h => h.ItemSize == 9)
                        ? GeometryFormat.RaidLegacy
                        : GeometryFormat.Payday
                    : Headers.Any(h => h.ItemSize == 9)
                        ? GeometryFormat.Raid
                        : GeometryFormat.Payday;

            foreach (var header in Headers)
            {
                Log.Default.Debug(
                    "Geometry header: Type={0} ({1}), ItemSize={2}, BytesPerVertex={3}",
                    header.ItemType,
                    Enum.IsDefined(
                        typeof(GeometryChannelTypes),
                        header.ItemType)
                        ? ((GeometryChannelTypes)header.ItemType).ToString()
                        : "UNKNOWN",
                    header.ItemSize,
                    GetItemSizeBytes(header));
            }

            foreach (GeometryHeader header in Headers)
                calc_size += GetItemSizeBytes(header);

            foreach (GeometryHeader head in Headers)
            {
                if (head.ItemType == GeometryChannelTypes.POSITION0)
                {
                    verts.Capacity = (int)vert_count + 1;

                    for (int x = 0; x < vert_count; x++)
                    {
                        verts.Add(
                            new Vector3(
                                instream.ReadSingle(),
                                instream.ReadSingle(),
                                instream.ReadSingle()));
                    }

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType == GeometryChannelTypes.NORMAL0)
                {
                    normals.Capacity = (int)vert_count + 1;

                    for (int x = 0; x < vert_count; x++)
                    {
                        normals.Add(
                            ReadVector3ByType(
                                instream,
                                head.ItemSize));
                    }

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType == GeometryChannelTypes.COLOR0)
                {
                    vertex_colors.Capacity =
                        (int)vert_count + 1;

                    for (int x = 0; x < vert_count; x++)
                        vertex_colors.Add(
                            new GeometryColor(instream));

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType == GeometryChannelTypes.BINORMAL0 || head.ItemType == GeometryChannelTypes.BINORMAL1)
                {
                    binormals.Capacity =
                        (int)vert_count + 1;

                    for (int x = 0; x < vert_count; x++)
                    {
                        binormals.Add(
                            ReadVector3ByType(
                                instream,
                                head.ItemSize));
                    }

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType == GeometryChannelTypes.TANGENT0 || head.ItemType == GeometryChannelTypes.TANGENT1)
                {
                    tangents.Capacity =
                        (int)vert_count + 1;

                    for (int x = 0; x < vert_count; x++)
                    {
                        tangents.Add(
                            ReadVector3ByType(
                                instream,
                                head.ItemSize));
                    }

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType ==
                         GeometryChannelTypes.BLENDINDICES0)
                {
                    weight_groups.Capacity =
                        (int)vert_count + 1;

                    for (int x = 0; x < vert_count; x++)
                    {
                        weight_groups.Add(
                            new GeometryWeightGroups(
                                instream));
                    }

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType ==
                         GeometryChannelTypes.BLENDWEIGHT0)
                {
                    if (head.ItemSize == 4)
                    {
                        Log.Default.Warn(
                            "Section {0} has four weights",
                            SectionId);
                    }

                    if (Format == GeometryFormat.RaidLegacy &&
                        head.ItemSize == 7)
                    {
                        raidLegacyBlendData.Capacity =
                            (int)vert_count;

                        for (int x = 0; x < vert_count; x++)
                        {

                            ushort value0 =
                                instream.ReadUInt16();

                            ushort value1 =
                                instream.ReadUInt16();

                            uint reserved =
                                instream.ReadUInt32();

                            raidLegacyBlendData.Add(
                                new RaidLegacyBlendData(
                                    value0,
                                    value1,
                                    reserved));
                        }

                        Log.Default.Debug(
                            "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                            head.ItemType,
                            head.ItemSize,
                            Format,
                            section.legacy,
                            GetItemSizeBytes(head));
                    }
                    else
                    {
                        weights.Capacity =
                            (int)vert_count + 1;

                        for (int x = 0; x < vert_count; x++)
                        {
                            Vector3 weights_entry =
                                new Vector3();

                            weights_entry.X =
                                instream.ReadSingle();

                            weights_entry.Y =
                                instream.ReadSingle();

                            if (head.ItemSize == 3)
                            {
                                weights_entry.Z =
                                    instream.ReadSingle();
                            }
                            else if (head.ItemSize == 4)
                            {
                                weights_entry.Z =
                                    instream.ReadSingle();

                                float tmp =
                                    instream.ReadSingle();

                                if (tmp != 0)
                                {
                                    Log.Default.Warn(
                                        "Nonzero fourth weight in {0} vtx {1} (is {2})",
                                        SectionId,
                                        x,
                                        tmp);
                                }
                            }
                            else if (head.ItemSize != 2)
                            {
                                throw new Exception(
                                    $"Bad BLENDWEIGHT0 item size {head.ItemSize}");
                            }

                            weights.Add(weights_entry);
                        }

                        Log.Default.Debug(
                            "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                            head.ItemType,
                            head.ItemSize,
                            Format,
                            section.legacy,
                            GetItemSizeBytes(head));
                    }
                }
                else if (head.ItemType >=
                         GeometryChannelTypes.TEXCOORD0 &&
                         head.ItemType <=
                         GeometryChannelTypes.TEXCOORD9)
                {
                    int idx =
                        head.ItemType -
                        GeometryChannelTypes.TEXCOORD0;

                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector2 uv;

                        if (head.ItemSize == 2)
                        {
                            uv = new Vector2(
                                instream.ReadSingle(),
                                -instream.ReadSingle());
                        }
                        else if (head.ItemSize == 9)
                        {
                            if (Format == GeometryFormat.Raid)
                            {
                                uv = new Vector2(
                                    instream.ReadSingle(),
                                    -instream.ReadSingle());
                            }
                            else
                            {
                                uv = new Vector2(
                                    ReadHalf(instream),
                                    -ReadHalf(instream));
                            }
                        }
                        else
                        {
                            throw new Exception(
                                $"Unsupported TEXCOORD type {head.ItemSize}");
                        }

                        UVs[idx].Add(uv);
                    }

                    Log.Default.Debug(
                        "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                        head.ItemType,
                        head.ItemSize,
                        Format,
                        section.legacy,
                        GetItemSizeBytes(head));
                }
                else if (head.ItemType ==
                         GeometryChannelTypes.POINTSIZE0)
                {
                    if (Format == GeometryFormat.RaidLegacy &&
                        head.ItemSize == 3)
                    {
                        point_sizes.Capacity =
                            (int)vert_count;

                        for (int x = 0; x < vert_count; x++)
                        {
                            float value1 =
                                instream.ReadSingle();

                            float value2 =
                                instream.ReadSingle();

                            float value3 =
                                instream.ReadSingle();

                            if (!float.IsFinite(value1) ||
                                !float.IsFinite(value2) ||
                                !float.IsFinite(value3))
                            {
                                throw new InvalidDataException(
                                    $"Invalid RaidLegacy POINTSIZE at vertex {x}: " +
                                    $"{value1}, {value2}, {value3}");
                            }

                            point_sizes.Add(value1);
                        }

                        Log.Default.Debug(
                            "{0} found: ItemSize={1}, Format={2}, Legacy={3}, bytes/vertex={4}",
                            head.ItemType,
                            head.ItemSize,
                            Format,
                            section.legacy,
                            GetItemSizeBytes(head));
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"Unsupported POINTSIZE item size {head.ItemSize} for format {Format}");
                    }
                }
                else
                {
                    throw new InvalidDataException(
                        $"Unsupported geometry channel type: {head.ItemType} " +
                        $"(type {(uint)head.ItemType}, item size {head.ItemSize})");
                }
            }

            HashName =
                new HashName(instream.ReadUInt64());

            remaining_data = null;

            long sect_end =
                section.offset + 12 + section.size;

            if (sect_end > instream.BaseStream.Position)
            {
                remaining_data =
                    instream.ReadBytes(
                        (int)(sect_end -
                              instream.BaseStream.Position));
            }
        }

        public bool HasHeader(GeometryChannelTypes type)
        {
            return Headers.Any(
                h => h.ItemType == type);
        }

        public override void StreamWriteData(
            BinaryWriter outstream)
        {
            outstream.Write(vert_count);
            outstream.Write(Headers.Count);

            foreach (GeometryHeader head in Headers)
            {
                outstream.Write(head.ItemSize);
                outstream.Write((uint)head.ItemType);
            }

            List<Vector3> verts = this.verts;
            int vert_pos = 0;

            List<Vector3> normals =
                this.normals.ToList();

            int norm_pos = 0;

            List<GeometryWeightGroups> weight_groups =
                this.weight_groups;

            int weight_groups_pos = 0;

            List<Vector3> binormals =
                this.binormals;

            List<Vector3> tangents =
                this.tangents;

            foreach (GeometryHeader head in Headers)
            {
                if (head.ItemType ==
                    GeometryChannelTypes.POSITION0)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 vert =
                            verts[vert_pos++];

                        outstream.Write(vert.X);
                        outstream.Write(vert.Y);
                        outstream.Write(vert.Z);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.NORMAL0)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 norm =
                            normals[norm_pos++];

                        WriteVector3ByType(
                            outstream,
                            norm,
                            head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.COLOR0)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        vertex_colors[x]
                            .StreamWrite(outstream);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.BINORMAL0 || head.ItemType == GeometryChannelTypes.BINORMAL1)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 value =
                            binormals.Count == vert_count
                                ? binormals[x]
                                : Vector3.Zero;

                        WriteVector3ByType(
                            outstream,
                            value,
                            head.ItemSize);
                    }
                }
                else if (head.ItemType == GeometryChannelTypes.TANGENT0 || head.ItemType == GeometryChannelTypes.TANGENT1)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector3 value =
                            tangents.Count == vert_count
                                ? tangents[x]
                                : Vector3.Zero;

                        WriteVector3ByType(
                            outstream,
                            value,
                            head.ItemSize);
                    }
                }
                else if (head.ItemType ==
                         GeometryChannelTypes.BLENDINDICES0)
                {
                    for (int x = 0; x < vert_count; x++)
                    {
                        if (weight_groups.Count != vert_count)
                        {
                            outstream.Write((ushort)0);
                            outstream.Write((ushort)0);
                            outstream.Write((ushort)0);
                            outstream.Write((ushort)0);
                        }
                        else
                        {
                            weight_groups[x]
                                .StreamWrite(outstream);

                            weight_groups_pos++;
                        }
                    }
                }
                else if (head.ItemType ==
                         GeometryChannelTypes.BLENDWEIGHT0)
                {
                    if (Format == GeometryFormat.RaidLegacy &&
                        head.ItemSize == 7)
                    {
                        if (raidLegacyBlendData.Count !=
                            vert_count)
                        {
                            throw new InvalidDataException(
                                "RaidLegacy BLENDWEIGHT0 data is missing.");
                        }

                        for (int x = 0; x < vert_count; x++)
                        {
                            RaidLegacyBlendData value =
                                raidLegacyBlendData[x];

                            outstream.Write(value.Value0);
                            outstream.Write(value.Value1);
                            outstream.Write(value.Reserved);
                        }
                    }
                    else
                    {
                        if (head.ItemSize == 4)
                        {
                            Log.Default.Warn(
                                "Section {0} has four weights",
                                HashName);
                        }

                        for (int x = 0; x < vert_count; x++)
                        {
                            Vector3 weight =
                                weights.Count == vert_count
                                    ? weights[x]
                                    : Vector3.UnitX;

                            outstream.Write(weight.X);
                            outstream.Write(weight.Y);

                            if (head.ItemSize == 3)
                            {
                                outstream.Write(weight.Z);
                            }
                            else if (head.ItemSize == 4)
                            {
                                outstream.Write(weight.Z);
                                outstream.Write(0.0f);
                            }
                            else if (head.ItemSize != 2)
                            {
                                throw new Exception(
                                    "Cannot write bad header BLENDWEIGHT s=" +
                                    head.ItemSize);
                            }
                        }
                    }
                }
                else if (head.ItemType >=
                         GeometryChannelTypes.TEXCOORD0 &&
                         head.ItemType <=
                         GeometryChannelTypes.TEXCOORD9)
                {
                    int idx =
                        head.ItemType -
                        GeometryChannelTypes.TEXCOORD0;

                    for (int x = 0; x < vert_count; x++)
                    {
                        Vector2 uv =
                            UVs[idx][x];

                        if (head.ItemSize == 2)
                        {
                            outstream.Write(uv.X);
                            outstream.Write(-uv.Y);
                        }
                        else if (head.ItemSize == 9)
                        {
                            if (Format == GeometryFormat.Raid)
                            {
                                outstream.Write(uv.X);
                                outstream.Write(-uv.Y);
                            }
                            else
                            {
                                WriteHalf(
                                    outstream,
                                    uv.X);

                                WriteHalf(
                                    outstream,
                                    -uv.Y);
                            }
                        }
                        else
                        {
                            throw new Exception(
                                $"Unsupported TEXCOORD type {head.ItemSize}");
                        }
                    }
                }
                else
                {
                    throw new InvalidDataException(
                        $"Unsupported geometry channel type: {(uint)head.ItemType} " +
                        $"(item size {head.ItemSize}, " +
                        $"{GetItemSizeBytes(head)} bytes/vertex, " +
                        $"format {Format})");
                }
            }

            outstream.Write(HashName.Hash);

            if (remaining_data != null)
                outstream.Write(remaining_data);
        }

        public void PrintDetailedOutput(
            StreamWriter outstream)
        {
            if (weight_groups.Count > 0 &&
                binormals.Count > 0 &&
                tangents.Count > 0 &&
                weights.Count > 0)
            {
                outstream.WriteLine(
                    "Printing weights table for " +
                    HashName);

                outstream.WriteLine(
                    "====================================================");

                outstream.WriteLine(
                    "unkn15_1\tunkn15_2\tunkn15_3\tunkn15_4\t" +
                    "unkn17_X\tunkn17_Y\tunk17_Z\ttotalsum\t" +
                    "unk_20_X\tunk_20_Y\tunk_20_Z\t" +
                    "unk21_X\tunk21_Y\tunk21_Z");

                for (int x = 0;
                     x < weight_groups.Count;
                     x++)
                {
                    outstream.WriteLine(
                        weight_groups[x].Bones1 + "\t" +
                        weight_groups[x].Bones2 + "\t" +
                        weight_groups[x].Bones3 + "\t" +
                        weight_groups[x].Bones4 + "\t" +
                        weights[x].X.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        weights[x].Y.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        weights[x].Z.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        (weights[x].X +
                         weights[x].Y +
                         weights[x].Z).ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        binormals[x].X.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        binormals[x].Y.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        binormals[x].Z.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        tangents[x].X.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        tangents[x].Y.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture) + "\t" +
                        tangents[x].Z.ToString(
                            "0.000000",
                            System.Globalization.CultureInfo.InvariantCulture));
                }

                outstream.WriteLine(
                    "====================================================");
            }
        }

        public override string ToString()
        {
            return base.ToString() +
                   " Count: " + vert_count +
                   " Headers: " + Headers.Count +
                   " Verts: " + verts.Count +
                   " UV0: " + uv0.Count +
                   " UV1: " + uv1.Count +
                   " Normals: " + normals.Count +
                   " Weight Groups: " + weight_groups.Count +
                   " Weights: " + weights.Count +
                   " Binormals: " + binormals.Count +
                   " Tangents: " + tangents.Count +
                   " unknown_hash: " + HashName +
                   (remaining_data != null
                       ? " REMAINING DATA! " +
                         remaining_data.Length +
                         " bytes"
                       : "");
        }
    }
}