using System.Collections.Generic;
using RimWorld.Planet;
using Verse;

namespace RimMind.Memory.Data
{
    public class RimMindMemoryWorldComponent : WorldComponent
    {
        private Dictionary<int, PawnMemoryStore> _pawnStores = new Dictionary<int, PawnMemoryStore>();
        private NarratorMemoryStore _narratorStore = new NarratorMemoryStore();

        private static RimMindMemoryWorldComponent? _instance;
        public static RimMindMemoryWorldComponent? Instance => _instance;

        public RimMindMemoryWorldComponent(World world) : base(world)
        {
            _instance = this;
        }

        public PawnMemoryStore GetOrCreatePawnStore(Pawn pawn)
        {
            int id = pawn.thingIDNumber;
            if (!_pawnStores.TryGetValue(id, out var store))
            {
                store = new PawnMemoryStore();
                _pawnStores[id] = store;
            }
            return store;
        }

        public NarratorMemoryStore NarratorStore => _narratorStore;

        public IEnumerable<PawnMemoryStore> AllPawnStores => _pawnStores.Values;

        public void ClearPawnStore(Pawn pawn) => _pawnStores.Remove(pawn.thingIDNumber);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref _pawnStores, "pawnStores", LookMode.Value, LookMode.Deep);
            _pawnStores ??= new Dictionary<int, PawnMemoryStore>();
            Scribe_Deep.Look(ref _narratorStore, "narratorStore");
            _narratorStore ??= new NarratorMemoryStore();
        }
    }
}
