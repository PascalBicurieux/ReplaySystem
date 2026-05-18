namespace ReplaySystem.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Text;
    using PlayerRoles;
    using ReplaySystem.Models;
    using ReplaySystem.Recording;
    using UnityEngine;

    public static class ReplaySerializer
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("RPLY");

        public static void Write(MultiRecorder rec, Stream output)
        {
            using (var gz = new GZipStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
            using (var w = new BinaryWriter(gz, Encoding.UTF8, leaveOpen: true))
            {
                w.Write(Magic);
                w.Write(ReplayBundle.CurrentVersion);
                w.Write(rec.ServerName ?? "Server");
                w.Write(rec.RoundId ?? string.Empty);
                w.Write(rec.StartedUtc.Ticks);
                w.Write(rec.EndedUtc.Ticks);
                w.Write(rec.MapSeed);
                w.Write(rec.Recorders.Count);
                foreach (var kv in rec.Recorders)
                {
                    WritePlayer(w, kv.Key, kv.Value);
                }
            }
        }

        public static ReplayBundle Read(Stream input)
        {
            var bundle = new ReplayBundle();
            using (var gz = new GZipStream(input, CompressionMode.Decompress, leaveOpen: true))
            using (var r = new BinaryReader(gz, Encoding.UTF8, leaveOpen: true))
            {
                byte[] magic = r.ReadBytes(4);
                if (magic.Length < 4 || magic[0] != Magic[0] || magic[1] != Magic[1] || magic[2] != Magic[2] || magic[3] != Magic[3])
                    throw new InvalidDataException("Invalid magic header — not a .replay.gz file.");

                bundle.Version = r.ReadByte();
                if (bundle.Version > ReplayBundle.CurrentVersion)
                    throw new InvalidDataException($"Replay version {bundle.Version} is newer than supported ({ReplayBundle.CurrentVersion}).");
                if (bundle.Version < ReplayBundle.CurrentVersion)
                    throw new InvalidDataException($"Replay version {bundle.Version} is too old (need {ReplayBundle.CurrentVersion}).");

                bundle.ServerName = r.ReadString();
                bundle.RoundId = r.ReadString();
                bundle.StartedUtc = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
                bundle.EndedUtc = new DateTime(r.ReadInt64(), DateTimeKind.Utc);
                bundle.MapSeed = r.ReadInt32();
                int playerCount = r.ReadInt32();
                for (int i = 0; i < playerCount; i++)
                    bundle.Players.Add(ReadPlayer(r));
            }
            return bundle;
        }

        private static void WritePlayer(BinaryWriter w, string userId, SimpleRecorder rec)
        {
            w.Write(userId ?? string.Empty);
            w.Write(rec.InitialNickname ?? string.Empty);
            w.Write((byte)rec.InitialRole);
            WriteVec3(w, rec.InitialPosition);
            w.Write(rec.InitialYaw);
            w.Write(rec.InitialPitch);

            w.Write(rec.Frames.Count);
            for (int i = 0; i < rec.Frames.Count; i++)
                WriteFrame(w, rec.Frames[i]);

            w.Write(rec.ItemEvents.Count);
            for (int i = 0; i < rec.ItemEvents.Count; i++)
                WriteItemEvent(w, rec.ItemEvents[i]);

            w.Write(rec.Explosions.Count);
            for (int i = 0; i < rec.Explosions.Count; i++)
                WriteExplosion(w, rec.Explosions[i]);

            w.Write(rec.Interactions.Count);
            for (int i = 0; i < rec.Interactions.Count; i++)
                WriteInteraction(w, rec.Interactions[i]);

            w.Write(rec.Deaths.Count);
            for (int i = 0; i < rec.Deaths.Count; i++)
                WriteDeath(w, rec.Deaths[i]);
        }

        private static SimpleRecorder ReadPlayer(BinaryReader r)
        {
            var rec = new SimpleRecorder();
            string userId = r.ReadString();
            string nickname = r.ReadString();
            byte role = r.ReadByte();
            Vector3 pos = ReadVec3(r);
            float yaw = r.ReadSingle();
            float pitch = r.ReadSingle();
            rec.HydrateInitial(userId, nickname, (RoleTypeId)role, pos, yaw, pitch);

            int frames = r.ReadInt32();
            for (int i = 0; i < frames; i++)
                rec.Frames.Add(ReadFrame(r));

            int items = r.ReadInt32();
            for (int i = 0; i < items; i++)
                rec.ItemEvents.Add(ReadItemEvent(r));

            int explosions = r.ReadInt32();
            for (int i = 0; i < explosions; i++)
                rec.Explosions.Add(ReadExplosion(r));

            int interactions = r.ReadInt32();
            for (int i = 0; i < interactions; i++)
                rec.Interactions.Add(ReadInteraction(r));

            int deaths = r.ReadInt32();
            for (int i = 0; i < deaths; i++)
                rec.Deaths.Add(ReadDeath(r));

            return rec;
        }

        private static void WriteFrame(BinaryWriter w, RecordedFrame f)
        {
            w.Write(f.Time);
            WriteVec3(w, f.Position);
            w.Write(f.Yaw);
            w.Write(f.Pitch);
            w.Write(f.MoveState);
            w.Write(f.Health);
            w.Write(f.MaxHealth);
            w.Write(f.ArtificialHealth);
            w.Write(f.MaxArtificialHealth);
            w.Write(f.CurrentRole);
            w.Write(f.IsCuffed);
            w.Write((int)f.CurrentItem);

            if (f.Inventory == null)
            {
                w.Write(-1);
            }
            else
            {
                w.Write(f.Inventory.Length);
                for (int i = 0; i < f.Inventory.Length; i++)
                    w.Write((int)f.Inventory[i]);
            }

            w.Write(f.WarheadStatus);
            w.Write(f.WarheadLever);
            w.Write(f.WarheadTimer);

            w.Write(f.Scp096RageState);
            w.Write(f.Scp096AbilityState);
            w.Write(f.Scp096EnragedTimeLeft);
            w.Write(f.Scp096TotalEnrageTime);
            w.Write(f.Scp096EnrageCooldown);
            w.Write(f.Scp096ChargeCooldown);
            w.Write(f.Scp096RemainingCharge);
            w.Write(f.Scp096TryNotToCry);

            w.Write(f.Scp3114DisguiseStatus);
            w.Write(f.Scp3114StolenRole);
            w.Write(f.Scp3114UnitId);
            w.Write(f.Scp3114DisguiseDuration);
            w.Write(f.Scp3114WarningTime);

            w.Write(f.Scp079CameraNetId);
            w.Write(f.Scp079Energy);
            w.Write(f.Scp079Level);
        }

        private static RecordedFrame ReadFrame(BinaryReader r)
        {
            var f = new RecordedFrame
            {
                Time = r.ReadSingle(),
                Position = ReadVec3(r),
                Yaw = r.ReadSingle(),
                Pitch = r.ReadSingle(),
                MoveState = r.ReadByte(),
                Health = r.ReadSingle(),
                MaxHealth = r.ReadSingle(),
                ArtificialHealth = r.ReadSingle(),
                MaxArtificialHealth = r.ReadSingle(),
                CurrentRole = r.ReadByte(),
                IsCuffed = r.ReadBoolean(),
                CurrentItem = (ItemType)r.ReadInt32(),
            };

            int invCount = r.ReadInt32();
            if (invCount >= 0)
            {
                f.Inventory = new ItemType[invCount];
                for (int i = 0; i < invCount; i++)
                    f.Inventory[i] = (ItemType)r.ReadInt32();
            }

            f.WarheadStatus = r.ReadByte();
            f.WarheadLever = r.ReadBoolean();
            f.WarheadTimer = r.ReadSingle();

            f.Scp096RageState = r.ReadByte();
            f.Scp096AbilityState = r.ReadByte();
            f.Scp096EnragedTimeLeft = r.ReadSingle();
            f.Scp096TotalEnrageTime = r.ReadSingle();
            f.Scp096EnrageCooldown = r.ReadSingle();
            f.Scp096ChargeCooldown = r.ReadSingle();
            f.Scp096RemainingCharge = r.ReadSingle();
            f.Scp096TryNotToCry = r.ReadBoolean();

            f.Scp3114DisguiseStatus = r.ReadByte();
            f.Scp3114StolenRole = r.ReadByte();
            f.Scp3114UnitId = r.ReadByte();
            f.Scp3114DisguiseDuration = r.ReadSingle();
            f.Scp3114WarningTime = r.ReadSingle();

            f.Scp079CameraNetId = r.ReadUInt32();
            f.Scp079Energy = r.ReadSingle();
            f.Scp079Level = r.ReadByte();

            return f;
        }

        private static void WriteItemEvent(BinaryWriter w, RecordedItemEvent ev)
        {
            w.Write(ev.Time);
            w.Write((byte)ev.Kind);
            w.Write((int)ev.ItemType);
            WriteVec3(w, ev.Position);
            WriteVec3(w, ev.Direction);
            w.Write(ev.Data);
        }

        private static RecordedItemEvent ReadItemEvent(BinaryReader r) => new RecordedItemEvent
        {
            Time = r.ReadSingle(),
            Kind = (ItemEventKind)r.ReadByte(),
            ItemType = (ItemType)r.ReadInt32(),
            Position = ReadVec3(r),
            Direction = ReadVec3(r),
            Data = r.ReadByte(),
        };

        private static void WriteExplosion(BinaryWriter w, RecordedExplosion ev)
        {
            w.Write(ev.Time);
            w.Write((int)ev.ProjectileType);
            WriteVec3(w, ev.Position);
            w.Write(ev.ExplosionType);
        }

        private static RecordedExplosion ReadExplosion(BinaryReader r) => new RecordedExplosion
        {
            Time = r.ReadSingle(),
            ProjectileType = (ItemType)r.ReadInt32(),
            Position = ReadVec3(r),
            ExplosionType = r.ReadByte(),
        };

        private static void WriteInteraction(BinaryWriter w, RecordedInteraction ev)
        {
            w.Write(ev.Time);
            w.Write((byte)ev.Kind);
            w.Write(ev.Allowed);
            w.Write(ev.TargetNetId);
            WriteVec3(w, ev.TargetPosition);
            w.Write(ev.Data);
            w.Write(ev.DataFloat);
        }

        private static RecordedInteraction ReadInteraction(BinaryReader r) => new RecordedInteraction
        {
            Time = r.ReadSingle(),
            Kind = (InteractionKind)r.ReadByte(),
            Allowed = r.ReadBoolean(),
            TargetNetId = r.ReadUInt32(),
            TargetPosition = ReadVec3(r),
            Data = r.ReadByte(),
            DataFloat = r.ReadSingle(),
        };

        private static void WriteDeath(BinaryWriter w, RecordedDeath ev)
        {
            w.Write(ev.Time);
            w.Write(ev.DamageType);
        }

        private static RecordedDeath ReadDeath(BinaryReader r) => new RecordedDeath
        {
            Time = r.ReadSingle(),
            DamageType = r.ReadByte(),
        };

        private static void WriteVec3(BinaryWriter w, Vector3 v)
        {
            w.Write(v.x); w.Write(v.y); w.Write(v.z);
        }

        private static Vector3 ReadVec3(BinaryReader r) => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
    }
}
