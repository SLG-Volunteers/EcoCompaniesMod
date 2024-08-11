
using Eco.Core.PropertyHandling;
using Eco.Core.Controller;

namespace Eco.Gameplay.Economy.Reputation
{
	using System.ComponentModel;
	using Eco.Shared.Localization;
	using Eco.Shared.Serialization;
	using Eco.Shared.Utils;
	using Range = Eco.Shared.Math.Range;

	[Serialized]
	public class CompanyPositiveReputationGiver : IGivesReputation
	{		
		public LocString MarkedUpName => TextLoc.InfoLightLocStr("AVG Positive Reputation");

		[Serialized] public int Id { get; set; } = RandomUtil.IntValue;

		float IGivesReputation.GivableReputationPerDay => float.PositiveInfinity;
		public float GivableReputationPerDayPerTarget => float.PositiveInfinity;
		public Range GivableReputationToSingleTargetTotal => new Range(0, float.PositiveInfinity);
		int IGivesReputation.DisplayPriority => 2;

		#region IController
		int controllerID;
		public ref int ControllerID => ref controllerID;

#pragma warning disable CS0067
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
		#endregion
	}

	[Serialized]
	public class CompanyNegativeReputationGiver : IGivesReputation
	{
		public LocString MarkedUpName => TextLoc.InfoLightLocStr("AVG Negative Reputation");

		[Serialized] public int Id { get; set; } = RandomUtil.IntValue;

		float IGivesReputation.GivableReputationPerDay => float.PositiveInfinity;
		public float GivableReputationPerDayPerTarget => float.PositiveInfinity;
		public Range GivableReputationToSingleTargetTotal => new Range(0, float.PositiveInfinity);
		int IGivesReputation.DisplayPriority => 2;

		#region IController
		int controllerID;
		public ref int ControllerID => ref controllerID;

#pragma warning disable CS0067
		public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067
		#endregion
	}
}
