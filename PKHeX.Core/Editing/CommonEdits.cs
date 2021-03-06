﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace PKHeX.Core
{
    /// <summary>
    /// Contains extension logic for modifying <see cref="PKM"/> data.
    /// </summary>
    public static class CommonEdits
    {
        /// <summary>
        /// Setting which enables/disables automatic manipulation of <see cref="PKM.Markings"/> when importing from a <see cref="ShowdownSet"/>.
        /// </summary>
        public static bool ShowdownSetIVMarkings { get; set; } = true;

        /// <summary>
        /// Default <see cref="MarkingMethod"/> when applying markings.
        /// </summary>
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
        public static Func<PKM, Func<int, int, int>> MarkingMethod { get; set; } = FlagHighLow;

        /// <summary>
        /// Sets the <see cref="PKM.Nickname"/> to the provided value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="nick"><see cref="PKM.Nickname"/> to set. If no nickname is provided, the <see cref="PKM.Nickname"/> is set to the default value for its current language and format.</param>
        public static void SetNickname(this PKM pk, string nick)
        {
            if (string.IsNullOrWhiteSpace(nick))
            {
                pk.ClearNickname();
                return;
            }
            pk.IsNicknamed = true;
            pk.Nickname = nick;
        }

        /// <summary>
        /// Clears the <see cref="PKM.Nickname"/> to the default value.
        /// </summary>
        /// <param name="pk"></param>
        public static string ClearNickname(this PKM pk)
        {
            pk.IsNicknamed = false;
            string nick = SpeciesName.GetSpeciesNameGeneration(pk.Species, pk.Language, pk.Format);
            pk.Nickname = nick;
            if (pk is GBPKM pk12)
                pk12.SetNotNicknamed();
            return nick;
        }

        /// <summary>
        /// Sets the <see cref="PKM.AltForm"/> value, with special consideration for <see cref="PKM.Format"/> values which derive the <see cref="PKM.AltForm"/> value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="form">Desired <see cref="PKM.AltForm"/> value to set.</param>
        public static void SetAltForm(this PKM pk, int form)
        {
            switch (pk.Format)
            {
                case 2:
                    while (pk.AltForm != form)
                        pk.SetRandomIVs();
                    break;
                case 3:
                    pk.SetPIDUnown3(form);
                    break;
                default:
                    pk.AltForm = form;
                    break;
            }
        }

        /// <summary>
        /// Sets the <see cref="PKM.Ability"/> value by sanity checking the provided <see cref="PKM.Ability"/> against the possible pool of abilities.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="abil">Desired <see cref="PKM.Ability"/> to set.</param>
        public static void SetAbility(this PKM pk, int abil)
        {
            if (abil < 0)
                return;
            var abilities = pk.PersonalInfo.Abilities;
            int abilIndex = Array.IndexOf(abilities, abil);
            abilIndex = Math.Max(0, abilIndex);
            pk.SetAbilityIndex(abilIndex);
        }

        /// <summary>
        /// Sets the <see cref="PKM.Ability"/> value based on the provided ability index (0-2)
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="abilIndex">Desired <see cref="PKM.AbilityNumber"/> (shifted by 1) to set.</param>
        public static void SetAbilityIndex(this PKM pk, int abilIndex)
        {
            if (pk is PK5 pk5 && abilIndex == 2)
                pk5.HiddenAbility = true;
            else if (pk.Format <= 5)
                pk.PID = PKX.GetRandomPID(pk.Species, pk.Gender, pk.Version, pk.Nature, pk.AltForm, (uint)(abilIndex * 0x10001));
            pk.RefreshAbility(abilIndex);
        }

        /// <summary>
        /// Sets a Random <see cref="PKM.EncryptionConstant"/> value. The <see cref="PKM.EncryptionConstant"/> is not updated if the value should match the <see cref="PKM.PID"/> instead.
        /// </summary>
        /// <remarks>Accounts for Wurmple evolutions.</remarks>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetRandomEC(this PKM pk)
        {
            int gen = pk.GenNumber;
            if (2 < gen && gen < 6)
            {
                pk.EncryptionConstant = pk.PID;
                return;
            }

            int wIndex = WurmpleUtil.GetWurmpleEvoGroup(pk.Species);
            if (wIndex != -1)
            {
                pk.EncryptionConstant = WurmpleUtil.GetWurmpleEC(wIndex);
                return;
            }
            pk.EncryptionConstant = Util.Rand32();
        }

        /// <summary>
        /// Sets the <see cref="PKM.IsShiny"/> derived value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="shiny">Desired <see cref="PKM.IsShiny"/> state to set.</param>
        /// <returns></returns>
        public static bool SetIsShiny(this PKM pk, bool shiny) => shiny ? SetShiny(pk) : pk.SetUnshiny();

        /// <summary>
        /// Makes a <see cref="PKM"/> shiny.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="xor0">Square shiny</param>
        /// <returns>Returns true if the <see cref="PKM"/> data was modified.</returns>
        public static bool SetShiny(PKM pk, bool xor0 = false)
        {
            if (pk.IsShiny)
                return false;

            while (true)
            {
                pk.SetShiny();

                if (pk.Format <= 7)
                    return true;

                var xor = pk.ShinyXor;
                if (xor0 ? xor == 0 : xor != 0)
                    return true;
            }
        }

        /// <summary>
        /// Makes a <see cref="PKM"/> not-shiny.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <returns>Returns true if the <see cref="PKM"/> data was modified.</returns>
        public static bool SetUnshiny(this PKM pk)
        {
            if (!pk.IsShiny)
                return false;

            pk.SetPIDGender(pk.Gender);
            return true;
        }

        /// <summary>
        /// Sets the <see cref="PKM.Nature"/> value, with special consideration for the <see cref="PKM.Format"/> values which derive the <see cref="PKM.Nature"/> value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="nature">Desired <see cref="PKM.Nature"/> value to set.</param>
        public static void SetNature(this PKM pk, int nature)
        {
            var value = Math.Min((int)Nature.Quirky, Math.Max((int)Nature.Hardy, nature));
            if (pk.Format >= 8)
                pk.StatNature = value;
            else if (pk.Format <= 4)
                pk.SetPIDNature(value);
            else
                pk.Nature = value;
        }

        /// <summary>
        /// Sets the <see cref="PKM.Nature"/> value, with special consideration for the <see cref="PKM.Format"/> values which derive the <see cref="PKM.Nature"/> value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="nature">Desired <see cref="PKM.Nature"/> value to set.</param>
        public static void SetNature(this PKM pk, Nature nature) => pk.SetNature((int) nature);

        /// <summary>
        /// Sets the individual PP Up count values depending if a Move is present in the moveslot or not.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="moves"><see cref="PKM.Moves"/> to use (if already known). Will fetch the current <see cref="PKM.Moves"/> if not provided.</param>
        public static void SetMaximumPPUps(this PKM pk, int[] moves)
        {
            pk.Move1_PPUps = GetPPUpCount(moves[0]);
            pk.Move2_PPUps = GetPPUpCount(moves[1]);
            pk.Move3_PPUps = GetPPUpCount(moves[2]);
            pk.Move4_PPUps = GetPPUpCount(moves[3]);

            pk.SetMaximumPPCurrent(moves);
            static int GetPPUpCount(int moveID) => moveID > 0 ? 3 : 0;
        }

        /// <summary>
        /// Sets the individual PP Up count values depending if a Move is present in the moveslot or not.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetMaximumPPUps(this PKM pk) => pk.SetMaximumPPUps(pk.Moves);

        /// <summary>
        /// Updates the <see cref="PKM.Moves"/> and updates the current PP counts.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="moves"><see cref="PKM.Moves"/> to set. Will be resized if 4 entries are not present.</param>
        /// <param name="maxPP">Option to maximize PP Ups</param>
        public static void SetMoves(this PKM pk, int[] moves, bool maxPP = false)
        {
            if (moves.Any(z => z > pk.MaxMoveID))
                moves = moves.Where(z => z <= pk.MaxMoveID).ToArray();
            if (moves.Length != 4)
                Array.Resize(ref moves, 4);

            pk.Moves = moves;
            if (maxPP)
                pk.SetMaximumPPUps(moves);
            else
                pk.SetMaximumPPCurrent(moves);
            pk.FixMoves();
        }

        /// <summary>
        /// Updates the individual PP count values for each moveslot based on the maximum possible value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="moves"><see cref="PKM.Moves"/> to use (if already known). Will fetch the current <see cref="PKM.Moves"/> if not provided.</param>
        public static void SetMaximumPPCurrent(this PKM pk, int[] moves)
        {
            pk.Move1_PP = moves.Length == 0 ? 0 : pk.GetMovePP(moves[0], pk.Move1_PPUps);
            pk.Move2_PP = moves.Length <= 1 ? 0 : pk.GetMovePP(moves[1], pk.Move2_PPUps);
            pk.Move3_PP = moves.Length <= 2 ? 0 : pk.GetMovePP(moves[2], pk.Move3_PPUps);
            pk.Move4_PP = moves.Length <= 3 ? 0 : pk.GetMovePP(moves[3], pk.Move4_PPUps);
        }

        /// <summary>
        /// Updates the individual PP count values for each moveslot based on the maximum possible value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetMaximumPPCurrent(this PKM pk) => pk.SetMaximumPPCurrent(pk.Moves);

        /// <summary>
        /// Sets the <see cref="PKM.Gender"/> value, with special consideration for the <see cref="PKM.Format"/> values which derive the <see cref="PKM.Gender"/> value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="gender">Desired <see cref="PKM.Gender"/> value to set.</param>
        public static void SetGender(this PKM pk, string gender)
        {
            int g = string.IsNullOrEmpty(gender)
                ? pk.GetSaneGender()
                : PKX.GetGenderFromString(gender);
            pk.SetGender(g);
        }

        /// <summary>
        /// Sets the <see cref="PKM.Gender"/> value, with special consideration for the <see cref="PKM.Format"/> values which derive the <see cref="PKM.Gender"/> value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="gender">Desired <see cref="PKM.Gender"/> value to set.</param>
        public static void SetGender(this PKM pk, int gender)
        {
            gender = Math.Min(2, Math.Max(0, gender));
            if (pk.Format <= 2)
            {
                pk.SetATKIVGender(gender);
            }
            else if (pk.Format <= 5)
            {
                pk.SetPIDGender(gender);
                pk.Gender = gender;
            }
            else
            {
                pk.Gender = gender;
            }
        }

        /// <summary>
        /// Fetches <see cref="PKM.RelearnMoves"/> based on the provided <see cref="LegalityAnalysis"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <returns><see cref="PKM.RelearnMoves"/> best suited for the current <see cref="PKM"/> data.</returns>
        public static IReadOnlyList<int> GetSuggestedRelearnMoves(this PKM pk) => new LegalityAnalysis(pk).GetSuggestedRelearnMoves();

        /// <summary>
        /// Fetches <see cref="PKM.RelearnMoves"/> based on the provided <see cref="LegalityAnalysis"/>.
        /// </summary>
        /// <param name="legal"><see cref="LegalityAnalysis"/> which contains parsed information pertaining to legality.</param>
        /// <returns><see cref="PKM.RelearnMoves"/> best suited for the current <see cref="PKM"/> data.</returns>
        public static IReadOnlyList<int> GetSuggestedRelearnMoves(this LegalityAnalysis legal)
        {
            var m = legal.GetSuggestedRelearn();
            if (m.Any(z => z != 0))
                return m;

            var enc = legal.EncounterMatch;
            if (enc is MysteryGift || enc is EncounterEgg)
                return m;

            var encounter = legal.GetSuggestedMetInfo();
            if (encounter is IRelearn r && r.Relearn.Length > 0)
                m = r.Relearn;

            return m;
        }

        /// <summary>
        /// Sanity checks the provided <see cref="PKM.Gender"/> value, and returns a sane value.
        /// </summary>
        /// <param name="pk"></param>
        /// <returns>Most-legal <see cref="PKM.Gender"/> value</returns>
        public static int GetSaneGender(this PKM pk)
        {
            int gt = pk.PersonalInfo.Gender;
            switch (gt)
            {
                case 255: return 2; // Genderless
                case 254: return 1; // Female-Only
                case 0: return 0; // Male-Only
            }
            if (!pk.IsGenderValid())
                return PKX.GetGenderFromPIDAndRatio(pk.PID, gt);
            return pk.Gender;
        }

        /// <summary>
        /// Copies <see cref="ShowdownSet"/> details to the <see cref="PKM"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="Set"><see cref="ShowdownSet"/> details to copy from.</param>
        public static void ApplySetDetails(this PKM pk, ShowdownSet Set)
        {
            pk.Species = Math.Min(pk.MaxSpeciesID, Set.Species);
            pk.SetMoves(Set.Moves, true);
            pk.ApplyHeldItem(Set.HeldItem, Set.Format);
            pk.CurrentLevel = Set.Level;
            pk.CurrentFriendship = Set.Friendship;
            pk.IVs = Set.IVs;
            pk.EVs = Set.EVs;

            pk.SetSuggestedHyperTrainingData(Set.IVs);
            if (ShowdownSetIVMarkings)
                pk.SetMarkings();

            pk.SetNickname(Set.Nickname);
            pk.SetAltForm(Set.FormIndex);
            pk.SetGender(Set.Gender);
            pk.SetMaximumPPUps(Set.Moves);
            pk.SetAbility(Set.Ability);
            pk.SetNature(Set.Nature);
            pk.SetIsShiny(Set.Shiny);
            pk.SetRandomEC();

            if (pk is IAwakened a)
            {
                a.SetSuggestedAwakenedValues(pk);
                if (pk is PB7 b)
                {
                    for (int i = 0; i < 6; i++)
                        pk.SetEV(i, 0);
                    b.ResetCalculatedValues();
                }
            }

            if (pk is IGigantamax c)
                c.CanGigantamax = Set.CanGigantamax;

            pk.ClearRecordFlags();
            pk.SetRecordFlags(Set.Moves);

            var legal = new LegalityAnalysis(pk);
            if (legal.Parsed && legal.Info.Relearn.Any(z => !z.Valid))
                pk.SetRelearnMoves(legal.GetSuggestedRelearnMoves());
            pk.ResetPartyStats();
            pk.RefreshChecksum();
        }

        /// <summary>
        /// Sets the <see cref="PKM.HeldItem"/> value depending on the current format and the provided item index &amp; format.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="item">Held Item to apply</param>
        /// <param name="format">Format required for importing</param>
        public static void ApplyHeldItem(this PKM pk, int item, int format)
        {
            item = ItemConverter.GetFormatHeldItemID(item, format, pk.Format);
            pk.HeldItem = ((uint)item > pk.MaxItemID) ? 0 : item;
        }

        /// <summary>
        /// Sets all Memory related data to the default value (zero).
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void ClearMemories(this PKM pk)
        {
            pk.OT_Memory = pk.OT_Affection = pk.OT_Feeling = pk.OT_Intensity = pk.OT_TextVar =
            pk.HT_Memory = pk.HT_Affection = pk.HT_Feeling = pk.HT_Intensity = pk.HT_TextVar = 0;
        }

        /// <summary>
        /// Sets the <see cref="PKM.Markings"/> to indicate flawless (or near-flawless) <see cref="PKM.IVs"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="ivs"><see cref="PKM.IVs"/> to use (if already known). Will fetch the current <see cref="PKM.IVs"/> if not provided.</param>
        public static void SetMarkings(this PKM pk, int[] ivs)
        {
            if (pk.Format <= 3)
                return; // no markings (gen3 only has 4; can't mark stats intelligently

            var markings = ivs.Select(MarkingMethod(pk)).ToArray();
            pk.Markings = PKX.ReorderSpeedLast(markings);
        }

        /// <summary>
        /// Sets the <see cref="PKM.Markings"/> to indicate flawless (or near-flawless) <see cref="PKM.IVs"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetMarkings(this PKM pk)
        {
            if (pk.Format <= 3)
                return; // no markings (gen3 only has 4; can't mark stats intelligently

            pk.SetMarkings(pk.IVs);
        }

        private static Func<int, int, int> FlagHighLow(PKM pk)
        {
            if (pk.Format < 7)
                return GetSimpleMarking;
            return GetComplexMarking;

            static int GetSimpleMarking(int val, int _) => val == 31 ? 1 : 0;
            static int GetComplexMarking(int val, int _)
            {
                if (val == 31 || val == 1)
                    return 1;
                if (val == 30 || val == 0)
                    return 2;
                return 0;
            }
        }

        /// <summary>
        /// Sets one of the <see cref="PKM.EVs"/> based on its index within the array.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="index">Index to set to</param>
        /// <param name="value">Value to set</param>
        public static void SetEV(this PKM pk, int index, int value)
        {
            switch (index)
            {
                case 0: pk.EV_HP = value; break;
                case 1: pk.EV_ATK = value; break;
                case 2: pk.EV_DEF = value; break;
                case 3: pk.EV_SPE = value; break;
                case 4: pk.EV_SPA = value; break;
                case 5: pk.EV_SPD = value; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Sets one of the <see cref="PKM.IVs"/> based on its index within the array.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="index">Index to set to</param>
        /// <param name="value">Value to set</param>
        public static void SetIV(this PKM pk, int index, int value)
        {
            switch (index)
            {
                case 0: pk.IV_HP = value; break;
                case 1: pk.IV_ATK = value; break;
                case 2: pk.IV_DEF = value; break;
                case 3: pk.IV_SPE = value; break;
                case 4: pk.IV_SPA = value; break;
                case 5: pk.IV_SPD = value; break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Updates the <see cref="PKM.IV_ATK"/> for a Generation 1/2 format <see cref="PKM"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="gender">Desired <see cref="PKM.Gender"/>.</param>
        public static void SetATKIVGender(this PKM pk, int gender)
        {
            while (pk.Gender != gender)
                pk.IV_ATK = Util.Rand.Next(pk.MaxIV + 1);
        }

        /// <summary>
        /// Fetches the highest value the provided <see cref="PKM.EVs"/> index can be while considering others.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="index">Index to fetch for</param>
        /// <returns>Highest value the value can be.</returns>
        public static int GetMaximumEV(this PKM pk, int index)
        {
            if (pk.Format < 3)
                return ushort.MaxValue;

            var EVs = pk.EVs;
            EVs[index] = 0;
            var sum = EVs.Sum();
            int remaining = 510 - sum;
            return Math.Min(Math.Max(remaining, 0), 252);
        }

        /// <summary>
        /// Fetches the highest value the provided <see cref="PKM.IVs"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="index">Index to fetch for</param>
        /// <param name="allow30">Causes the returned value to be dropped down -1 if the value is already at a maxmimum.</param>
        /// <returns>Highest value the value can be.</returns>
        public static int GetMaximumIV(this PKM pk, int index, bool allow30 = false)
        {
            if (pk.GetIV(index) == pk.MaxIV && allow30)
                return pk.MaxIV - 1;
            return pk.MaxIV;
        }

        /// <summary>
        /// Sets the <see cref="PKM.IVs"/> to match a provided <see cref="hptype"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="hptype">Desired Hidden Power typing.</param>
        public static void SetHiddenPower(this PKM pk, int hptype)
        {
            var IVs = pk.IVs;
            HiddenPower.SetIVsForType(hptype, pk.IVs, pk.Format);
            pk.IVs = IVs;
        }

        /// <summary>
        /// Sets the <see cref="PKM.IVs"/> to match a provided <see cref="hptype"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="hptype">Desired Hidden Power typing.</param>
        public static void SetHiddenPower(this PKM pk, MoveType hptype) => pk.SetHiddenPower((int) hptype);

        /// <summary>
        /// Sets the Technical Record flags for the <see cref="pk"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="value">Value to set for the record.</param>
        /// <param name="max">Max record to set.</param>
        public static void SetRecordFlags(this PKM pk, bool value, int max = 100)
        {
            if (!(pk is PK8 pk8))
                return;
            for (int i = 0; i < max; i++)
                pk8.SetMoveRecordFlag(i, value);
        }

        /// <summary>
        /// Clears the Technical Record flags for the <see cref="pk"/>.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void ClearRecordFlags(this PKM pk) => pk.SetRecordFlags(false, 112);

        /// <summary>
        /// Sets the Technical Record flags for the <see cref="pk"/> based on the current moves.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="moves">Moves to set flags for. If a move is not a Technical Record, it is skipped.</param>
        public static void SetRecordFlags(this PKM pk, IEnumerable<int> moves)
        {
            if (!(pk is PK8 pk8))
                return;
            var permit = pk8.PersonalInfo.TMHM;
            foreach (var m in moves)
            {
                var index = Array.IndexOf(Legal.TMHM_SWSH, m, 100);
                if (index < 100)
                    continue;
                if (permit[index])
                    pk8.SetMoveRecordFlag(index - 100, true);
            }
        }

        /// <summary>
        /// Sets all the Technical Record flags for the <see cref="pk"/> if they are permitted to be learned in-game.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetRecordFlags(this PKM pk)
        {
            if (!(pk is PK8 pk8))
                return;
            var permit = pk8.PersonalInfo.TMHM;
            for (int i = 100; i < permit.Length; i++)
            {
                if (permit[i])
                    pk8.SetMoveRecordFlag(i - 100, true);
            }
        }

        /// <summary>
        /// Force hatches a PKM by applying the current species name and a valid Met Location from the origin game.
        /// </summary>
        /// <param name="pk">PKM to apply hatch details to</param>
        /// <param name="reHatch">Re-hatch already hatched <see cref="PKM"/> inputs</param>
        public static void ForceHatchPKM(this PKM pk, bool reHatch = false)
        {
            if (!pk.IsEgg && !reHatch)
                return;
            pk.IsEgg = false;
            pk.ClearNickname();
            pk.CurrentFriendship = pk.PersonalInfo.BaseFriendship;
            if (pk.IsTradedEgg)
                pk.Egg_Location = pk.Met_Location;
            var loc = EncounterSuggestion.GetSuggestedEggMetLocation(pk);
            if (loc >= 0)
                pk.Met_Location = loc;
            pk.MetDate = DateTime.Today;
            if (pk.Gen6)
                pk.SetHatchMemory6();
        }

        /// <summary>
        /// Force hatches a PKM by applying the current species name and a valid Met Location from the origin game.
        /// </summary>
        /// <param name="pk">PKM to apply hatch details to</param>
        /// <param name="origin">Game the egg originated from</param>
        /// <param name="dest">Game the egg is currently present on</param>
        public static void SetEggMetData(this PKM pk, GameVersion origin, GameVersion dest)
        {
            bool traded = origin == dest;
            var today = pk.MetDate = DateTime.Today;
            pk.Egg_Location = EncounterSuggestion.GetSuggestedEncounterEggLocationEgg(pk, traded);
            pk.EggMetDate = today;
        }

        /// <summary>
        /// Maximizes the <see cref="PKM.CurrentFriendship"/>. If the <see cref="PKM.IsEgg"/>, the hatch counter is set to 1.
        /// </summary>
        /// <param name="pk">PKM to apply hatch details to</param>
        public static void MaximizeFriendship(this PKM pk)
        {
            if (pk.IsEgg)
                pk.OT_Friendship = 1;
            else
                pk.CurrentFriendship = byte.MaxValue;
            if (pk is PB7 pb)
                pb.ResetCP();
        }

        /// <summary>
        /// Maximizes the <see cref="PKM.CurrentLevel"/>. If the <see cref="PKM.IsEgg"/>, the <see cref="PKM"/> is ignored.
        /// </summary>
        /// <param name="pk">PKM to apply hatch details to</param>
        public static void MaximizeLevel(this PKM pk)
        {
            if (pk.IsEgg)
                return;
            pk.CurrentLevel = 100;
            if (pk is PB7 pb)
                pb.ResetCP();
        }

        /// <summary>
        /// Gets a moveset for the provided <see cref="PKM"/> data.
        /// </summary>
        /// <param name="pk">PKM to generate for</param>
        /// <param name="random">Full movepool &amp; shuffling</param>
        /// <returns>4 moves</returns>
        public static int[] GetMoveSet(this PKM pk, bool random = false)
        {
            var la = new LegalityAnalysis(pk);
            var moves = pk.GetMoveSet(la, random);

            if (random)
                return moves;

            var clone = pk.Clone();
            clone.SetMoves(moves);
            var newLa = new LegalityAnalysis(pk);

            // ReSharper disable once TailRecursiveCall
            return newLa.Valid ? moves : GetMoveSet(pk, true);
        }

        /// <summary>
        /// Gets a moveset for the provided <see cref="PKM"/> data.
        /// </summary>
        /// <param name="pk">PKM to generate for</param>
        /// <param name="la">Precomputed optional</param>
        /// <param name="random">Full movepool &amp; shuffling</param>
        /// <returns>4 moves</returns>
        public static int[] GetMoveSet(this PKM pk, LegalityAnalysis la, bool random = false)
        {
            int[] m = la.GetSuggestedMoves(tm: random, tutor: random, reminder: random);
            if (m == null)
                return pk.Moves;

            if (!m.All(z => la.AllSuggestedMovesAndRelearn().Contains(z)))
                m = m.Intersect(la.AllSuggestedMovesAndRelearn()).ToArray();

            if (random)
                Util.Shuffle(m);

            const int count = 4;
            if (m.Length > count)
                return m.SliceEnd(m.Length - count);
            Array.Resize(ref m, count);
            return m;
        }

        /// <summary>
        /// Toggles the marking at a given index.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="index">Marking index to toggle</param>
        /// <param name="markings">Current marking values (optional)</param>
        /// <returns>Current marking values</returns>
        public static int[] ToggleMarking(this PKM pk, int index, int[] markings)
        {
            switch (pk.Format)
            {
                case 3:
                case 4:
                case 5:
                case 6: // on/off
                    markings[index] ^= 1; // toggle
                    pk.Markings = markings;
                    break;
                case 7: // 0 (none) | 1 (blue) | 2 (pink)
                case 8:
                    markings[index] = (markings[index] + 1) % 3; // cycle 0->1->2->0...
                    pk.Markings = markings;
                    break;
            }
            return markings;
        }

        /// <summary>
        /// Toggles the marking at a given index.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="index">Marking index to toggle</param>
        /// <returns>Current marking values</returns>
        public static int[] ToggleMarking(this PKM pk, int index) => pk.ToggleMarking(index, pk.Markings);

        /// <summary>
        /// Sets the Memory details to a Hatched Egg's memories.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetHatchMemory6(this PKM pk)
        {
            pk.OT_Memory = 2;
            pk.OT_Affection = 0;
            pk.OT_Feeling = Memories.GetRandomFeeling(pk.OT_Memory);
            pk.OT_Intensity = 1;
            pk.OT_TextVar = pk.XY ? 43 : 27; // riverside road : battling spot
        }

        /// <summary>
        /// Sets a random memory specific to <see cref="GameVersion.Gen6"/> locality.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetRandomMemory6(this PKM pk)
        {
            // for lack of better randomization :)
            pk.OT_Memory = 63;
            pk.OT_Intensity = 6;
            pk.OT_Feeling = Memories.GetRandomFeeling(pk.OT_Memory);
        }

        /// <summary>
        /// Sets the <see cref="PKM.Nickname"/> to its default value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        /// <param name="la">Precomputed optional</param>
        public static void SetDefaultNickname(this PKM pk, LegalityAnalysis la)
        {
            if (la.Parsed && la.EncounterOriginal is EncounterTrade t && t.HasNickname)
                pk.SetNickname(t.GetNickname(pk.Language));
            else
                pk.ClearNickname();
        }

        /// <summary>
        /// Sets the <see cref="PKM.Nickname"/> to its default value.
        /// </summary>
        /// <param name="pk">Pokémon to modify.</param>
        public static void SetDefaultNickname(this PKM pk) => pk.SetDefaultNickname(new LegalityAnalysis(pk));

        private static readonly string[] PotentialUnicode = { "★☆☆☆", "★★☆☆", "★★★☆", "★★★★" };
        private static readonly string[] PotentialNoUnicode = { "+", "++", "+++", "++++" };

        /// <summary>
        /// Gets the Potential evaluation of the input <see cref="pk"/>.
        /// </summary>
        /// <param name="pk">Pokémon to analyze.</param>
        /// <param name="unicode">Returned value is unicode or not</param>
        /// <returns>Potential string</returns>
        public static string GetPotentialString(this PKM pk, bool unicode = true)
        {
            var arr = unicode ? PotentialUnicode : PotentialNoUnicode;
            return arr[pk.PotentialRating];
        }

        // Extensions
        /// <summary>
        /// Gets the Location Name for the <see cref="PKM"/>
        /// </summary>
        /// <param name="pk">PKM to fetch data for</param>
        /// <param name="eggmet">Location requested is the egg obtained location, not met location.</param>
        /// <returns>Location string</returns>
        public static string GetLocationString(this PKM pk, bool eggmet)
        {
            if (pk.Format < 2)
                return string.Empty;

            int locval = eggmet ? pk.Egg_Location : pk.Met_Location;
            return GameInfo.GetLocationName(eggmet, locval, pk.Format, pk.GenNumber, (GameVersion)pk.Version);
        }
    }
}
