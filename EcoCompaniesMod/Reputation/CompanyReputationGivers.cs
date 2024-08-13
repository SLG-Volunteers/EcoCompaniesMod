namespace Eco.Gameplay.Economy.Reputation
{
	using Shared.Localization;
	using Shared.Serialization;
	using Shared.Utils;
	using Range = Shared.Math.Range;

	[Serialized]
	public class CompanyPositiveReputationGiver : IGivesReputation
	{		
		public LocString MarkedUpName => TextLoc.InfoLightLocStr(Text.Color(Color.LightGreen, "Average Employee Reputation"));

		[Serialized] public int Id { get; set; } = RandomUtil.IntValue;

		float IGivesReputation.GivableReputationPerDay => float.PositiveInfinity;
		public float GivableReputationPerDayPerTarget => float.PositiveInfinity;
		public Range GivableReputationToSingleTargetTotal => new(0, float.PositiveInfinity);
		int IGivesReputation.DisplayPriority => 2;

		#region IController
		int controllerID;
		public ref int ControllerID => ref controllerID;
		#endregion
	}

	[Serialized]
	public class CompanyNegativeReputationGiver : IGivesReputation
	{
		public LocString MarkedUpName => TextLoc.InfoLightLocStr(Text.Color(Color.LightRed, "Average Employee Reputation"));

		[Serialized] public int Id { get; set; } = RandomUtil.IntValue;

		float IGivesReputation.GivableReputationPerDay => float.PositiveInfinity;
		public float GivableReputationPerDayPerTarget => float.PositiveInfinity;
		public Range GivableReputationToSingleTargetTotal => new(0, float.PositiveInfinity);
		int IGivesReputation.DisplayPriority => 2;

		#region IController
		int controllerID;
		public ref int ControllerID => ref controllerID;
		#endregion
	}
}