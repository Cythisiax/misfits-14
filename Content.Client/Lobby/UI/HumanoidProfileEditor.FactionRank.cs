// #Cythisiax Added - Faction rank picker: a rank dropdown next to NCR / Enclave jobs in the job list.
// Only ranks the character has unlocked (playtime or leadership) can be picked; locked ones show the
// hours they need, or that command is holding them back. Nobody moves up unless they pick it here.

using System.Linq;
using Content.Client._Misfits.FactionRank;
using Content.Shared._Misfits.FactionRank;
using Content.Shared.Roles;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI
{
    public sealed partial class HumanoidProfileEditor
    {
        private FactionRankPreferenceSystem? _rankPreferences;

        private FactionRankPreferenceSystem RankPreferences
        {
            get
            {
                if (_rankPreferences != null)
                    return _rankPreferences;

                _rankPreferences = _entManager.System<FactionRankPreferenceSystem>();
                _rankPreferences.OptionsUpdated += name =>
                {
                    if (Profile?.Name == name)
                        RefreshJobs();
                };
                return _rankPreferences;
            }
        }

        private void AddRankPicker(BoxContainer jobContainer, JobPrototype job)
        {
            if (Profile == null || string.IsNullOrWhiteSpace(Profile.Name))
                return;

            var prefs = RankPreferences;
            if (prefs.GetPinLadder(job.ID) is not { } ladder)
                return;

            if (!prefs.TryGetOptions(Profile.Name, out var options))
                return;

            var ladderOptions = options.Ladders.FirstOrDefault(l => l.Ladder == ladder.ID);
            if (ladderOptions?.Held is not { } held)
                return;

            var characterName = Profile.Name;
            var picker = new OptionButton
            {
                MinWidth = 190,
                VerticalAlignment = VAlignment.Center,
                Margin = new(3f, 3f, 3f, 0f),
                ToolTip = Loc.GetString("faction-rank-picker-tooltip"),
            };

            for (var i = 0; i < ladderOptions.Ranks.Count; i++)
            {
                var option = ladderOptions.Ranks[i];
                var name = _prototypeManager.TryIndex<FactionRankPrototype>(option.Rank, out var rank) ? rank.Name : option.Rank;
                var label = option.Unlocked
                    ? name
                    : option.Capped
                        ? Loc.GetString("faction-rank-picker-capped", ("rank", name))
                        : Loc.GetString("faction-rank-picker-locked", ("rank", name), ("hours", option.MinHours));

                picker.AddItem(label, i);
                picker.SetItemDisabled(i, !option.Unlocked);

                if (option.Rank == held)
                    picker.SelectId(i);
            }

            picker.OnItemSelected += args =>
            {
                picker.SelectId(args.Id);
                prefs.SetRank(characterName, ladder.ID, ladderOptions.Ranks[args.Id].Rank);
            };

            jobContainer.AddChild(picker);
        }
    }
}
