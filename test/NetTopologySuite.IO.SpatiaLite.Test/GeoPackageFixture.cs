using System;
using System.Configuration;
using System.Data.SQLite;
using System.IO;
using NetTopologySuite.Geometries;
using NUnit.Framework;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    [TestFixture]
    [Category("Database.IO")]
    public class GeoPackageFixture : AbstractIOFixture
    {
        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XY;
            Compressed = false;
        }

        public bool InputHasZ => (InputOrdinates & Ordinates.Z) == Ordinates.Z;

        public bool InputHasM => (InputOrdinates & Ordinates.M) == Ordinates.M;

        public bool Compressed { get; set; }

        protected virtual string Name { get { return "GeoPackageXY.sqlite"; } }

        protected override void AddAppConfigSpecificItems(KeyValueConfigurationCollection kvcc)
        {
            //kvcc.Add("SpatiaLiteCompressed", "false");
        }

        protected override void ReadAppConfigInternal(KeyValueConfigurationCollection kvcc)
        {
            //this.Compressed = bool.Parse(kvcc["SpatiaLiteCompressed"].Value);
        }

        protected override void CreateTestStore()
        {
            if (File.Exists(Name))
                File.Delete(Name);

            using (var conn = new SQLiteConnection($"Data Source=\"{Name}\""))
            {
                conn.Open();
                conn.EnableExtensions(true);
                SpatialiteLoader.Load(conn);
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText =
                        "CREATE TABLE \"nts_io_geopackage\" (id int primary key, wkt text, the_geom blob);";
                    cmd.ExecuteNonQuery();
                }
            }
        }

        protected override void CheckEquality(Geometry gIn, Geometry gParsed, WKTWriter writer)
        {
            var res = gIn.EqualsExact(gParsed);
            if (res) return;

            if (Compressed)
            {
                var discreteHausdorffDistance =
                    Algorithm.Distance.DiscreteHausdorffDistance.Distance(gIn, gParsed);
                if (discreteHausdorffDistance > 0.05)
                {
                    Console.WriteLine();
                    Console.WriteLine(gIn.AsText());
                    Console.WriteLine(gParsed.AsText());
                    Console.WriteLine("DiscreteHausdorffDistance=" + discreteHausdorffDistance);
                }
                Assert.IsTrue(discreteHausdorffDistance < 0.001);
            }
            else Assert.IsTrue(false);
        }

        protected override Geometry Read(byte[] bytes)
        {
            var reader = new GeoPackageGeoReader();
            return reader.Read(bytes);
        }

        protected override byte[] Write(Geometry geom)
        {
            var writer = new GeoPackageGeoWriter
            {
                HandleOrdinates = ClipOrdinates
            };
            return writer.Write(geom);
        }
    }

    [TestFixture]
    [Category("Database.IO")]
    public class GeoPackageFixture3D : GeoPackageFixture
    {
        protected override string Name => "GeoPackage3D.sqlite";

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XYZ;
        }
    }

    [TestFixture]
    [Category("Database.IO")]
    public class GeoPackageFixtureM : GeoPackageFixture
    {
        protected override string Name => "GeoPackageM.sqlite";

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XYM;
        }
    }

    [TestFixture]
    [Category("Database.IO")]
    public class GeoPackageFixture3DM : GeoPackageFixture
    {
        protected override string Name => "GeoPackage3DM.sqlite";

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XYZM;
        }
    }

    [TestFixture]
    [Category("Database.IO")]
    public class GeoPackageFixture3DMClippedTo2D : GeoPackageFixture
    {
        protected override string Name => "GeoPackageFixture3DMClippedTo2D.sqlite";

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XYZM;
            ClipOrdinates = Ordinates.XY;
        }
    }
}
