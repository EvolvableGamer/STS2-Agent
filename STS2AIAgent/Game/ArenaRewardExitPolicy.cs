namespace STS2AIAgent.Game;
internal static class ArenaRewardExitPolicy
{
 public static bool Allows(bool rewardScreen,bool pendingCardChoice,bool canProceed,bool hasClaimableReward)=>rewardScreen&&!pendingCardChoice&&canProceed&&!hasClaimableReward;
}
