namespace NetTopologySuite.IO.SpatiaLite.Test
{
    using System;
    using System.Configuration;
    using System.Data;
    using System.Data.SQLite;
    using System.IO;
    using NetTopologySuite.Geometries;
    using NUnit.Framework;

    [NUnit.Framework.TestFixture]
    [NUnit.Framework.Category("Database.IO")]
    public class SpatiaLiteFixture : AbstractIOFixture
    {
        private int _counter;

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XY;
            Compressed = false;
        }

        public bool Compressed { get; set; }

        protected virtual string Name { get { return "SpatiaLite.sqlite"; } }

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

            using var conn = new SQLiteConnection("Data Source=\"" + Name + "\"");
            conn.Open();
            conn.EnableExtensions(true);
            SpatialiteLoader.Load(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "CREATE TABLE \"nts_io_spatialite\" (id int primary key, the_geom geometry);";
            cmd.ExecuteNonQuery();
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
            else
                Assert.IsTrue(false);
        }

        protected override Geometry Read(byte[] b)
        {
            return new GaiaGeoReader().Read(b);
        }

        protected override byte[] Write(Geometry gIn)
        {
            var writer = new GaiaGeoWriter();
            writer.HandleOrdinates = ClipOrdinates;
            writer.UseCompressed = Compressed;

            byte[] b = writer.Write(gIn);

            using var conn = new SQLiteConnection("Data Source=\"" + Name + "\"");
            conn.Open();
            conn.EnableExtensions(true);
            SpatialiteLoader.Load(conn);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO \"nts_io_spatialite\" VALUES(@id, @g);";
            var idParameter = cmd.Parameters.Add("id", DbType.Int32);
            idParameter.Value = ++_counter;

            var geometryParameter = cmd.Parameters.Add("g", DbType.Object);
            geometryParameter.Value = b;

            cmd.ExecuteNonQuery();

            cmd.Parameters.Clear();
            cmd.CommandText = "SELECT 'XY' || CASE WHEN ST_Is3D(the_geom) = 1 THEN 'Z' ELSE '' END || CASE WHEN ST_IsMeasured(the_geom) = 1 THEN 'M' ELSE '' END FROM \"nts_io_spatialite\" WHERE id = @id";
            cmd.Parameters.Add(idParameter);

            string ordinates = $"{cmd.ExecuteScalar()}";
            Assert.That(Enum.Parse<Ordinates>(ordinates), Is.EqualTo(InputOrdinates & ClipOrdinates));
            return b;
        }
    }

    [NUnit.Framework.TestFixture]
    [NUnit.Framework.Category("Database.IO")]
    public class SpatiaLiteFixtureCompressed : SpatiaLiteFixture
    {
        protected override string Name { get { return "SpatiaLiteCompressed.sqlite"; } }

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            Compressed = true;
        }
    }

    [NUnit.Framework.TestFixture]
    [NUnit.Framework.Category("Database.IO")]
    public class SpatiaLiteFixture3D : SpatiaLiteFixture
    {
        protected override string Name { get { return "SpatiaLite3D.sqlite"; } }

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XYZ;
        }
    }

    [NUnit.Framework.TestFixture]
    [NUnit.Framework.Category("Database.IO")]
    public class SpatiaLiteFixture3DCompressed : SpatiaLiteFixture3D
    {
        protected override string Name { get { return "SpatiaLite3DCompressed.sqlite"; } }

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            Compressed = true;
        }
    }

    [NUnit.Framework.TestFixture]
    [NUnit.Framework.Category("Database.IO")]
    public class SpatiaLiteFixture3DMClippedTo2D : SpatiaLiteFixture
    {
        protected override string Name { get { return "SpatiaLiteFixture3DMClippedTo2D.sqlite"; } }

        public override void OnFixtureSetUp()
        {
            base.OnFixtureSetUp();
            InputOrdinates = Ordinates.XYZM;
            ClipOrdinates = Ordinates.XY;
        }
    }
}
