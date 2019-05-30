// Copyright (c) NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Represents GeoPackage binary header.
    /// </summary>
    /// <remarks>
    /// Code adapted from SharpMap, used with special permission.
    /// </remarks>
    /// <seealso href="http://www.geopackage.org/spec/#gpb_format"/>
    internal class GeoPackageBinaryHeader
    {
        private const byte IsEmptyFlag = 0x01 << 4;
        private const byte EndianessFlag = 0x01;
        private const byte ExtentFlags = 0x07 << 1;

        internal enum GeoPackageBinaryType : byte
        {
            Standard, Extended
        }

        private byte[] _magic = new byte[] { 0x47, 0x50 };
        private byte _version;
        private byte _flags;
        private int _srs_id;
        private Envelope _extent;
        private Interval _zrange;
        private Interval _mrange;

        public static GeoPackageBinaryHeader Read(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var header = new GeoPackageBinaryHeader
            {
                _magic = reader.ReadBytes(2),
                _version = reader.ReadByte(),
                _flags = reader.ReadByte()
            };

            bool swap = header.Endianess == 0;

            int srsid = swap
                ? SwapByteOrder(reader.ReadInt32())
                : reader.ReadInt32();
            header._srs_id = srsid;

            var ordinates = header.Ordinates;
            if (ordinates == Ordinates.None)
            {
                header._extent = new Envelope(
                    double.MinValue, double.MaxValue,
                    double.MinValue, double.MaxValue);
                header._zrange = Interval.Create(double.MinValue, double.MaxValue);
                header._mrange = Interval.Create(double.MinValue, double.MaxValue);
                return header;
            }

            double minx = swap
                ? SwapByteOrder(reader.ReadDouble())
                : reader.ReadDouble();
            double maxx = swap
                ? SwapByteOrder(reader.ReadDouble())
                : reader.ReadDouble();
            double miny = swap
                ? SwapByteOrder(reader.ReadDouble())
                : reader.ReadDouble();
            double maxy = swap
                ? SwapByteOrder(reader.ReadDouble())
                : reader.ReadDouble();
            header._extent = new Envelope(minx, maxx, miny, maxy);

            if ((ordinates & Ordinates.Z) == Ordinates.Z)
            {
                double min = swap
                    ? SwapByteOrder(reader.ReadDouble())
                    : reader.ReadDouble();
                double max = swap
                    ? SwapByteOrder(reader.ReadDouble())
                    : reader.ReadDouble();
                var range = Interval.Create(min, max);
                header._zrange = range;
            }

            if ((ordinates & Ordinates.M) == Ordinates.M)
            {
                double min = swap
                    ? SwapByteOrder(reader.ReadDouble())
                    : reader.ReadDouble();
                double max = swap
                    ? SwapByteOrder(reader.ReadDouble())
                    : reader.ReadDouble();
                var range = Interval.Create(min, max);
                header._mrange = range;
            }

            return header;
        }

        public static void Write(BinaryWriter writer, GeoPackageBinaryHeader header)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            writer.Write(header._magic);
            writer.Write(header._version);
            writer.Write(header._flags);

            bool swap = header.Endianess == 0;

            int srsid = swap
                ? SwapByteOrder(header._srs_id)
                : header._srs_id;
            writer.Write(srsid);

            var ordinates = header.Ordinates;
            if (ordinates == Ordinates.None)
            {
                return;
            }

            var envelope = header._extent;
            double minx = swap
                ? SwapByteOrder(envelope.MinX)
                : envelope.MinX;
            double maxx = swap
                ? SwapByteOrder(envelope.MaxX)
                : envelope.MaxX;
            double miny = swap
                ? SwapByteOrder(envelope.MinY)
                : envelope.MinY;
            double maxy = swap
                ? SwapByteOrder(envelope.MaxY)
                : envelope.MaxY;
            writer.Write(minx);
            writer.Write(maxx);
            writer.Write(miny);
            writer.Write(maxy);

            if ((ordinates & Ordinates.Z) == Ordinates.Z)
            {
                var range = header._zrange;
                double min = swap
                    ? SwapByteOrder(range.Min)
                    : range.Min;
                writer.Write(min);
                double max = swap
                    ? SwapByteOrder(range.Max)
                    : range.Max;
                writer.Write(max);
            }

            if ((ordinates & Ordinates.M) == Ordinates.M)
            {
                var range = header._mrange;
                double min = swap
                    ? SwapByteOrder(range.Min)
                    : range.Min;
                writer.Write(min);
                double max = swap
                    ? SwapByteOrder(range.Max)
                    : range.Max;
                writer.Write(max);
            }
        }

        private static int SwapByteOrder(int value)
        {
            unchecked
            {
                return (int)SwapByteOrder((uint)value);
            }
        }

        private static double SwapByteOrder(double val)
        {
            return BitConverter.Int64BitsToDouble(SwapByteOrder(BitConverter.DoubleToInt64Bits(val)));
        }

        private static uint SwapByteOrder(uint value)
        {
            return (value & 0x000000FF) << 24 |
                   (value & 0x0000FF00) << 8 |
                   (value & 0x00FF0000) >> 8 |
                   (value & 0xFF000000) >> 24;
        }


        private static long SwapByteOrder(long value)
        {
            unchecked
            {
                return (long)SwapByteOrder((ulong)value);
            }
        }

        private static ulong SwapByteOrder(ulong value)
        {
            return (value & 0x00000000000000FF) << 56 |
                   (value & 0x000000000000FF00) << 40 |
                   (value & 0x0000000000FF0000) << 24 |
                   (value & 0x00000000FF000000) << 8 |
                   (value & 0x000000FF00000000) >> 8 |
                   (value & 0x0000FF0000000000) >> 24 |
                   (value & 0x00FF000000000000) >> 40 |
                   (value & 0xFF00000000000000) >> 56;
        }

        /// <summary>
        /// Gets a value indicating the ordinates
        /// </summary>
        public Ordinates Ordinates
        {
            get
            {
                switch ((_flags & ExtentFlags) >> 1)
                {
                    case 0:
                        return Ordinates.None;
                    case 1:
                        return Ordinates.XY;
                    case 2:
                        return Ordinates.XYZ;
                    case 3:
                        return Ordinates.XYM;
                    case 4:
                        return Ordinates.XYZM;
                    default:
                        throw new ArgumentOutOfRangeException("ExtentFlags");
                }
            }
        }

        internal int NumOrdinates
        {
            get
            {
                switch ((_flags & ExtentFlags) >> 1)
                {
                    case 0:
                        return 0;
                    case 1:
                        return 2;
                    case 2:
                    case 3:
                        return 3;
                    case 4:
                        return 4;
                    default:
                        throw new ArgumentOutOfRangeException("NumOrdinates");
                }
            }
        }

        /// <summary>
        /// Gets a value indicating that this geometry is empty
        /// </summary>
        public bool IsEmpty => (_flags & IsEmptyFlag) == IsEmptyFlag;

        /// <summary>
        /// Gets a value indicating byte order (0=Big Endian, 1= Little Indian).
        /// </summary>
        /// <see href="http://www.geopackage.org/spec120/#gpb_format"/>
        public int Endianess => _flags & EndianessFlag;

        /// <summary>
        /// Gets the magic number
        /// </summary>
        public byte[] Magic => _magic;

        /// <summary>
        /// Gets a value indicating the version of the geometry data
        /// </summary>
        public byte Version => _version;

        /// <summary>
        /// Gets a value indicating the spatial reference id
        /// </summary>
        public int SrsId
        {
            get => _srs_id;
            internal set => _srs_id = value;
        }

        public Envelope Extent
        {
            get => _extent;
            internal set => _extent = value;
        }

        public byte Flags
        {
            get => _flags;
            internal set => _flags = value;
        }

        public Interval ZRange => _zrange;

        public Interval MRange => _mrange;
    }
}
