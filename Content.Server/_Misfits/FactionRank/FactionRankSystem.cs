// #Cythisiax Added - Faction rank system: replaces NCRRankPinSpawnSystem and RankTitleSystem.
// Where the prefix comes from depends on the faction:
//  - Pin factions (NCR, Enclave): the pin is the rank. Each character has a saved row per ladder:
//    the rank they chose to wear, plus whatever leadership set through the roster. The highest they
//    can wear is what playtime earns, or what leadership granted if that's higher. A leadership
//    demotion is a hard cap until leadership changes it again. Nobody gets moved up on their own:
//    the first time they spawn they wear the highest they've earned, after that they stay put and
//    get told when a higher rank is open, and they pick it in the lobby if they want it.
//    Whoever wears the pin, on the neck or attached to their uniform/armor, gets its prefix.
//  - No-pin factions (Rangers, Legion, BoS): the job slot is the rank and it's stamped on their
//    ID tag at spawn. Whoever has that ID tag gets its prefix.
// A worn pin wins over the ID tag. The ID's job title stays as-is either way.
// In chat/radio the prefix goes in front of their name ("1SG John Doe"), but only when they're
// speaking as themselves — masked, disguised or no-ID speakers keep whatever name they show as.
// The prefix shows in the faction's radio channel color, so rank and radio read the same.

using System.Linq;
using System.Threading.Tasks;
using Content.Server.Access.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.IdentityManagement;
using Content.Server.Inventory;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Misfits.Clothing.Pins;
using Content.Shared._Misfits.FactionRank;
using Content.Shared.Chat;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.FactionRank;

public sealed class FactionRankSystem : EntitySystem
{
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTime = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    private const string InsigniaSlot = "neck";

    private readonly Dictionary<string, FactionRankPrototype> _ranksByInsignia = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<HumanoidAppearanceComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName,
            after: new[] { typeof(IdentitySystem), typeof(ServerInventorySystem) });
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        SubscribeNetworkEvent<RequestFactionRankOptionsEvent>(OnRequestOptions);
        SubscribeNetworkEvent<SetFactionRankEvent>(OnSetRank);

        BuildInsigniaLookup();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<FactionRankPrototype>())
            BuildInsigniaLookup();
    }

    private void BuildInsigniaLookup()
    {
        _ranksByInsignia.Clear();
        foreach (var rank in _proto.EnumeratePrototypes<FactionRankPrototype>())
        {
            if (rank.Insignia is { } insignia)
                _ranksByInsignia[insignia.Id] = rank;
        }
    }

    #region Spawn

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !TryGetLadder(args.JobId, out var ladder))
            return;

        if (IsPinLadder(ladder))
        {
            var holder = EnsureComp<FactionRankHolderComponent>(args.Mob);
            holder.Ladder = ladder.ID;
            holder.UserId = args.Player.UserId;
            holder.CharacterName = args.Profile.Name;
            LoadAndApplyPin(args.Mob, args.Player, ladder);
            return;
        }

        var earned = EarnedIndex(args.Player, ladder);
        if (earned >= 0 &&
            _proto.TryIndex(ladder.Thresholds[earned].Rank, out var rank) &&
            _idCard.TryFindIdCard(args.Mob, out var idCard))
        {
            var comp = EnsureComp<FactionRankComponent>(idCard);
            comp.Rank = rank.ID;
            Dirty(idCard, comp);
        }
    }

    private async void LoadAndApplyPin(EntityUid mob, ICommonSession player, RankLadderPrototype ladder)
    {
        var characterName = Comp<FactionRankHolderComponent>(mob).CharacterName;
        FactionCharacterRank? row = null;

        try
        {
            var rows = await _db.GetFactionCharacterRanksAsync(player.UserId.UserId, characterName);
            row = rows.FirstOrDefault(r => r.Ladder == ladder.ID);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load faction rank for {player.Name} ({characterName}): {e}");
        }

        if (TerminatingOrDeleted(mob) || !TryComp<FactionRankHolderComponent>(mob, out var holder))
            return;

        holder.GrantedRank = row?.GrantedRank;
        holder.GrantCapped = row?.GrantCapped ?? false;

        var max = MaxIndex(ladder, EarnedIndex(player, ladder), holder.GrantedRank, holder.GrantCapped);
        if (max < 0)
            return;

        var held = RankIndex(ladder, row?.HeldRank);
        var save = false;
        if (held < 0 || held > max)
        {
            held = max;
            save = true;
        }

        holder.HeldRank = ladder.Thresholds[held].Rank.Id;
        if (save)
            SaveHolder(holder);

        ApplyPin(mob, ladder, held);

        if (held < max && _proto.TryIndex(ladder.Thresholds[max].Rank, out var available))
        {
            _chat.DispatchServerMessage(player,
                Loc.GetString("faction-rank-available", ("rank", available.Name)));
        }
    }

    #endregion

    #region Ladders

    public bool TryGetLadder(ProtoId<JobPrototype> job, out RankLadderPrototype ladder)
    {
        foreach (var proto in _proto.EnumeratePrototypes<RankLadderPrototype>())
        {
            if (!proto.Jobs.Contains(job))
                continue;

            ladder = proto;
            return true;
        }

        ladder = default!;
        return false;
    }

    public bool IsPinLadder(RankLadderPrototype ladder)
    {
        return ladder.Thresholds.Any(t => _proto.TryIndex(t.Rank, out var rank) && rank.Insignia != null);
    }

    private static int RankIndex(RankLadderPrototype ladder, string? rank)
    {
        return rank == null ? -1 : ladder.Thresholds.FindIndex(t => t.Rank.Id == rank);
    }

    private int EarnedIndex(ICommonSession player, RankLadderPrototype ladder)
    {
        var seconds = 0;
        if (_playTime.TryGetTrackerTimes(player, out var times))
            seconds = GetTrackerSeconds(times, ladder);

        var index = -1;
        for (var i = 0; i < ladder.Thresholds.Count; i++)
        {
            if (seconds >= ladder.Thresholds[i].Min)
                index = i;
        }

        return index;
    }

    private static int MaxIndex(RankLadderPrototype ladder, int earned, string? granted, bool capped)
    {
        var grant = RankIndex(ladder, granted);
        if (grant < 0)
            return earned;

        return capped ? grant : Math.Max(grant, earned);
    }

    #endregion

    #region Roster

    public bool TryGetPinStanding(EntityUid body, out string rankName, out int index, out int count)
    {
        rankName = string.Empty;
        index = -1;
        count = 0;

        if (!TryComp<FactionRankHolderComponent>(body, out var holder) ||
            !_proto.TryIndex(holder.Ladder, out var ladder))
        {
            return false;
        }

        index = RankIndex(ladder, holder.HeldRank);
        if (index < 0 || !_proto.TryIndex(ladder.Thresholds[index].Rank, out var rank))
            return false;

        rankName = rank.Name;
        count = ladder.Thresholds.Count;
        return true;
    }

    public bool TryStepPinRank(EntityUid body, ICommonSession session, bool promote, out string newRankName)
    {
        newRankName = string.Empty;

        if (!TryComp<FactionRankHolderComponent>(body, out var holder) ||
            !_proto.TryIndex(holder.Ladder, out var ladder))
        {
            return false;
        }

        var held = RankIndex(ladder, holder.HeldRank);
        var next = promote ? held + 1 : held - 1;
        if (held < 0 || next < 0 || next >= ladder.Thresholds.Count ||
            !_proto.TryIndex(ladder.Thresholds[next].Rank, out var rank))
        {
            return false;
        }

        holder.GrantedRank = rank.ID;
        holder.GrantCapped = !promote && next < EarnedIndex(session, ladder);
        holder.HeldRank = rank.ID;
        SaveHolder(holder);
        ApplyPin(body, ladder, next);

        newRankName = rank.Name;
        return true;
    }

    private async void SaveHolder(FactionRankHolderComponent holder)
    {
        try
        {
            await _db.UpsertFactionCharacterRankAsync(new FactionCharacterRank
            {
                PlayerId = holder.UserId.UserId,
                CharacterName = holder.CharacterName,
                Ladder = holder.Ladder.Id,
                HeldRank = holder.HeldRank,
                GrantedRank = holder.GrantedRank,
                GrantCapped = holder.GrantCapped,
            });
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save faction rank for {holder.CharacterName}: {e}");
        }
    }

    #endregion

    #region Lobby

    private async void OnRequestOptions(RequestFactionRankOptionsEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var rows = await LoadRows(session, ev.CharacterName);
        RaiseNetworkEvent(new FactionRankOptionsEvent(ev.CharacterName, BuildOptions(session, rows)), session);
    }

    private async void OnSetRank(SetFactionRankEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_proto.TryIndex<RankLadderPrototype>(ev.Ladder, out var ladder) || !IsPinLadder(ladder))
            return;

        var rows = await LoadRows(session, ev.CharacterName);
        var row = rows.FirstOrDefault(r => r.Ladder == ladder.ID);

        var max = MaxIndex(ladder, EarnedIndex(session, ladder), row?.GrantedRank, row?.GrantCapped ?? false);
        var pick = RankIndex(ladder, ev.Rank);
        if (pick < 0 || pick > max)
            return;

        row ??= new FactionCharacterRank
        {
            PlayerId = session.UserId.UserId,
            CharacterName = ev.CharacterName,
            Ladder = ladder.ID,
        };
        row.HeldRank = ev.Rank;

        try
        {
            await _db.UpsertFactionCharacterRankAsync(row);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save faction rank pick for {session.Name} ({ev.CharacterName}): {e}");
            return;
        }

        rows.RemoveAll(r => r.Ladder == ladder.ID);
        rows.Add(row);
        RaiseNetworkEvent(new FactionRankOptionsEvent(ev.CharacterName, BuildOptions(session, rows)), session);
    }

    private async Task<List<FactionCharacterRank>> LoadRows(ICommonSession session, string characterName)
    {
        try
        {
            return await _db.GetFactionCharacterRanksAsync(session.UserId.UserId, characterName);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load faction ranks for {session.Name} ({characterName}): {e}");
            return new List<FactionCharacterRank>();
        }
    }

    private List<FactionRankLadderOptions> BuildOptions(ICommonSession session, List<FactionCharacterRank> rows)
    {
        var result = new List<FactionRankLadderOptions>();
        foreach (var ladder in _proto.EnumeratePrototypes<RankLadderPrototype>())
        {
            if (!IsPinLadder(ladder))
                continue;

            var row = rows.FirstOrDefault(r => r.Ladder == ladder.ID);
            var capped = row?.GrantCapped ?? false;
            var earned = EarnedIndex(session, ladder);
            var max = MaxIndex(ladder, earned, row?.GrantedRank, capped);

            var held = RankIndex(ladder, row?.HeldRank);
            if (held < 0 || held > max)
                held = max;

            var ranks = new List<FactionRankOption>();
            for (var i = 0; i < ladder.Thresholds.Count; i++)
            {
                ranks.Add(new FactionRankOption(
                    ladder.Thresholds[i].Rank.Id,
                    i <= max,
                    ladder.Thresholds[i].Min / 3600,
                    capped && i > max && i <= earned));
            }

            result.Add(new FactionRankLadderOptions(
                ladder.ID,
                ladder.Jobs.Select(j => j.Id).ToList(),
                ranks,
                held >= 0 ? ladder.Thresholds[held].Rank.Id : null));
        }

        return result;
    }

    #endregion

    #region Prefix

    public FactionRankPrototype? GetRank(EntityUid wearer)
    {
        if (TryGetWornPinRank(wearer, out var pinRank))
            return pinRank;

        if (_idCard.TryFindIdCard(wearer, out var idCard) &&
            TryComp<FactionRankComponent>(idCard, out var comp) &&
            comp.Rank != null &&
            _proto.TryIndex(comp.Rank.Value, out var idRank))
        {
            return idRank;
        }

        return null;
    }

    private void OnTransformSpeakerName(Entity<HumanoidAppearanceComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (args.VoiceName != Name(ent))
            return;

        var rank = GetRank(ent);
        if (rank == null || string.IsNullOrWhiteSpace(rank.Prefix))
            return;

        args.VoiceName = $"{rank.Prefix} {args.VoiceName}";
        args.RankPrefix = rank.Prefix;
        args.RankColor = rank.Color;
    }

    private void OnExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        var rank = GetRank(ent);
        if (rank == null)
            return;

        var rankText = rank.Paygrade == null
            ? rank.Name
            : $"{rank.Name} ({rank.Paygrade})";

        using (args.PushGroup(nameof(FactionRankSystem)))
        {
            args.PushMarkup(Loc.GetString("faction-rank-examine",
                ("user", Identity.Entity(ent, EntityManager)),
                ("rank", FormattedMessage.EscapeText(rankText))));
        }
    }

    #endregion

    #region Pins

    private void ApplyPin(EntityUid mob, RankLadderPrototype ladder, int index)
    {
        if (!_proto.TryIndex(ladder.Thresholds[index].Rank, out var rank) || rank.Insignia is not { } insignia)
            return;

        var ladderRanks = ladder.Thresholds.Select(t => t.Rank.Id).ToHashSet();
        foreach (var pin in WornPins(mob).ToList())
        {
            if (TryGetPinRank(pin, out var pinRank) && pinRank.ID != rank.ID && ladderRanks.Contains(pinRank.ID))
                Del(pin);
        }

        if (WornPins(mob).Any(p => MetaData(p).EntityPrototype?.ID == insignia.Id))
            return;

        var newPin = Spawn(insignia, Transform(mob).Coordinates);

        if (!_inventory.TryGetSlotEntity(mob, InsigniaSlot, out _) &&
            _inventory.TryEquip(mob, newPin, InsigniaSlot, silent: true, force: true))
        {
            return;
        }

        _hands.TryPickupAnyHand(mob, newPin, checkActionBlocker: false);
    }

    private IEnumerable<EntityUid> WornPins(EntityUid wearer)
    {
        if (_inventory.TryGetSlotEntity(wearer, InsigniaSlot, out var neckItem))
            yield return neckItem.Value;

        var slots = _inventory.GetSlotEnumerator(wearer, SlotFlags.INNERCLOTHING | SlotFlags.OUTERCLOTHING);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } clothing ||
                !TryComp<ClothingPinHolderComponent>(clothing, out var holder) ||
                !_containers.TryGetContainer(clothing, holder.ContainerId, out var container))
            {
                continue;
            }

            foreach (var pin in container.ContainedEntities)
                yield return pin;
        }
    }

    private bool TryGetWornPinRank(EntityUid wearer, out FactionRankPrototype rank)
    {
        foreach (var pin in WornPins(wearer))
        {
            if (TryGetPinRank(pin, out rank))
                return true;
        }

        rank = default!;
        return false;
    }

    private bool TryGetPinRank(EntityUid pin, out FactionRankPrototype rank)
    {
        rank = default!;
        if (TerminatingOrDeleted(pin))
            return false;

        var protoId = MetaData(pin).EntityPrototype?.ID;
        return protoId != null && _ranksByInsignia.TryGetValue(protoId, out rank!);
    }

    #endregion

    private int GetTrackerSeconds(Dictionary<string, TimeSpan> times, RankLadderPrototype ladder)
    {
        if (string.IsNullOrEmpty(ladder.Tracker))
            return 0;

        if (!ladder.DepartmentTracker)
            return times.TryGetValue(ladder.Tracker, out var t) ? (int) t.TotalSeconds : 0;

        if (!_proto.TryIndex<DepartmentPrototype>(ladder.Tracker, out var dept))
            return 0;

        var total = TimeSpan.Zero;
        foreach (var role in dept.Roles)
        {
            var trackerId = _proto.TryIndex<JobPrototype>(role, out var jobProto)
                ? jobProto.PlayTimeTracker.Id
                : role.Id;
            if (times.TryGetValue(trackerId, out var roleTime))
                total += roleTime;
        }

        return (int) total.TotalSeconds;
    }
}
