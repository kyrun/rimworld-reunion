using System;
using RimWorld;
using Verse;

namespace Kyrun.Reunion
{
	class Mod : Verse.Mod
	{
		const int INPUT_MIN_DAYS_BTWN_EVENTS = 0;
		const int INPUT_MAX_DAYS_BTWN_EVENTS = GenDate.DaysPerYear;

		Settings _settings;

		// Constructor
		public Mod(ModContentPack content) : base(content)
		{
			_settings = GetSettings<Settings>();
		}

		// Mod UI
		public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
		{
			const float GAP_HEIGHT = 12f;

			Listing_Standard listingStandard = new Listing_Standard();
			listingStandard.Begin(inRect);

			var prevMin = _settings.minDaysBetweenEvents;
			var strMinDaysBtwn = _settings.minDaysBetweenEvents.ToString();
			listingStandard.TextFieldNumericLabeled("Reunion.MinDaysBetweenEvents".Translate(), ref _settings.minDaysBetweenEvents, ref strMinDaysBtwn, INPUT_MIN_DAYS_BTWN_EVENTS, INPUT_MAX_DAYS_BTWN_EVENTS);
			_settings.minDaysBetweenEvents = UnityEngine.Mathf.RoundToInt(listingStandard.Slider(_settings.minDaysBetweenEvents, INPUT_MIN_DAYS_BTWN_EVENTS, INPUT_MAX_DAYS_BTWN_EVENTS));

			listingStandard.Gap(GAP_HEIGHT);

			var prevMax = _settings.maxDaysBetweenEvents;
			var strMaxDaysBtwn = _settings.maxDaysBetweenEvents.ToString();
			listingStandard.TextFieldNumericLabeled("Reunion.MaxDaysBetweenEvents".Translate(), ref _settings.maxDaysBetweenEvents, ref strMaxDaysBtwn, INPUT_MIN_DAYS_BTWN_EVENTS + 1, INPUT_MAX_DAYS_BTWN_EVENTS);
			_settings.maxDaysBetweenEvents = UnityEngine.Mathf.RoundToInt(listingStandard.Slider(_settings.maxDaysBetweenEvents, INPUT_MIN_DAYS_BTWN_EVENTS + 1, INPUT_MAX_DAYS_BTWN_EVENTS));

			if (_settings.minDaysBetweenEvents >= _settings.maxDaysBetweenEvents)
			{
				_settings.minDaysBetweenEvents = _settings.maxDaysBetweenEvents - 1;
			}

			if (prevMin != _settings.minDaysBetweenEvents || prevMax != _settings.maxDaysBetweenEvents)
			{
				GameComponent.TryScheduleNextEvent(true);
			}

			listingStandard.Gap(GAP_HEIGHT);

			foreach (Settings.Event evtType in Enum.GetValues(typeof(Settings.Event)))
			{
				var allow = _settings.EventAllow[evtType];
				listingStandard.CheckboxLabeled(Settings.CreateTranslationKey(evtType).Translate(), ref allow);
				_settings.EventAllow[evtType] = allow;
			}

			listingStandard.End();

			base.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "Reunion";
		}
	}
}
