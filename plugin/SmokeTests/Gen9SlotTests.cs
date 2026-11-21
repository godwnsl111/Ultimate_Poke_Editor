using PKHeX.Core;
using UltimateEditor;

static class Gen9SlotTests
{
    static void Check(bool value, string message)
    { if (!value) throw new Exception(message); Console.WriteLine("PASS GEN9 SLOTS: " + message); }
    public static void Run()
    {
        foreach (var version in new[] { GameVersion.SL, GameVersion.VL, GameVersion.ZA })
        {
            var fixture = BlankSaveFile.Get(version, "TEST", LanguageID.English);
            // BlankBlocks contains size-only (None) entries, not serializable
            // game block types. Give those synthetic blocks explicit types.
            var blocks = ((ISCBlockArray)fixture).AllBlocks.Select(b => b.Type != SCTypeCode.None ? b.Clone() :
                (SCBlock)Activator.CreateInstance(typeof(SCBlock), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new object[] { b.Key, b.Data.Length == 0 ? SCTypeCode.Bool1 : SCTypeCode.Object, new Memory<byte>(b.Data.ToArray()) }, null)!).ToArray();
            var fixtureData = SwishCrypto.Encrypt(blocks);
            SaveFile save = version == GameVersion.ZA ? new SAV9ZA(fixtureData) : new SAV9SV(fixtureData);
            Span<byte> storage = save is SAV9SV sv ? sv.BoxInfo.Data : ((SAV9ZA)save).BoxInfo.Data;
            storage.Clear(); // A game's raw-zero empty records are not decrypted PKM zeroes.
            if (version == GameVersion.ZA)
            {
                for (int i = 0; i < save.SlotCount; i++)
                    storage[save.GetBoxSlotOffset(i) + save.SIZE_PARTY] = 1;
                storage[save.GetBoxSlotOffset(0) + save.SIZE_BOXSLOT - 1] = 0xA5;
                storage[save.GetBoxSlotOffset(1) + save.SIZE_BOXSLOT - 1] = 0xA5;
                int available = Enumerable.Range(0, save.SlotCount).Count(i => !save.IsBoxSlotLocked(i) && !save.IsBoxSlotOverwriteProtected(i));
                Check(InjectionPlan.EmptySlots(save, 0).Count == available, "ZA DLC empty records with present=1 and footer metadata recognized; protected slots excluded");
            }
            Check(InjectionPlan.EmptySlots(save, 0).Contains(1), version + " raw-zero empty slot recognized");
            save.BlankPKM.WriteEncryptedDataParty(storage.Slice(save.GetBoxSlotOffset(1), save.SIZE_BOXSLOT));
            Check(InjectionPlan.EmptySlots(save, 0).Contains(1), version + " encrypted blank record recognized");
            storage[save.GetBoxSlotOffset(2) + save.SIZE_PARTY - 1] = 1;
            Check(!InjectionPlan.EmptySlots(save, 0).Contains(2), version + " unexplained bytes preserved");
            var existing = save.BlankPKM;
            existing.Species = 25; existing.EncryptionConstant = 0x12345678; existing.RefreshChecksum();
            existing.WriteEncryptedDataParty(storage.Slice(save.GetBoxSlotOffset(3), save.SIZE_BOXSLOT));
            Check(!InjectionPlan.EmptySlots(save, 0).Contains(3), version + " occupied record preserved");
            var tail = InjectionPlan.EmptySlots(save, save.BoxCount - 1);
            Check(tail[0] / save.BoxSlotCount == save.BoxCount - 1 && tail.Contains(1), version + " box scan wraps correctly");

            var before = InjectionPlan.Fingerprint(save);
            var route = EncounterCatalog.Build(version).Selected.First(r => r.Kind == RouteKind.Gift && r.Form == 0);
            var pk = EncounterCatalog.GetTemplate(route).ConvertToPKM(new SimpleTrainerInfo(save, version));
            pk.RefreshChecksum();
            Check(new LegalityAnalysis(pk).Valid, version + " generated entity is legal");
            var clone = InjectionPlan.CloneSave(save);
            int destination = InjectionPlan.EmptySlots(clone, 0)[0];
            clone.SetBoxSlotAtIndex(pk, destination, EntityImportSettings.None);
            if (version == GameVersion.ZA)
            {
                ReadOnlySpan<byte> updated = ((SAV9ZA)clone).BoxInfo.Data;
                int footer = save.GetBoxSlotOffset(destination) + save.SIZE_PARTY;
                Check(storage.Slice(footer, save.SIZE_BOXSLOT - save.SIZE_PARTY).SequenceEqual(updated.Slice(footer, save.SIZE_BOXSLOT - save.SIZE_PARTY)),
                    "ZA insertion preserves destination slot footer metadata");
            }
            // Blank SC fixtures omit optional blocks that SaveUtil's real-save
            // length detector expects; reparse them with their concrete format.
            var bytes = clone.Write().ToArray();
            SaveFile parsed = version == GameVersion.ZA ? new SAV9ZA(bytes) : new SAV9SV(bytes);
            Check(parsed.GetBoxSlotAtIndex(destination).Species == pk.Species && new LegalityAnalysis(parsed.GetBoxSlotAtIndex(destination)).Valid,
                version + " generated entity survives serialized box roundtrip");
            ReadOnlySpan<byte> after = parsed is SAV9SV parsedSV ? parsedSV.BoxInfo.Data : ((SAV9ZA)parsed).BoxInfo.Data;
            for (int i = 0; i < save.SlotCount; i++)
                if (i != destination && !storage.Slice(save.GetBoxSlotOffset(i), save.SIZE_BOXSLOT).SequenceEqual(after.Slice(save.GetBoxSlotOffset(i), save.SIZE_BOXSLOT)))
                    throw new Exception(version + " untouched slot changed");
            Check(before.SequenceEqual(InjectionPlan.Fingerprint(save)), version + " all untouched slots and live input preserved");
            storage.Fill(1);
            Check(InjectionPlan.EmptySlots(save, 0).Count == 0, version + " full or corrupt records never treated as empty");
        }
    }
}
