using System.Collections.Generic;

namespace KidsSafeDesktop.Models;

/// <summary>一节眼保健操的步骤。</summary>
public class EyeExerciseStep
{
    public string Name { get; init; } = string.Empty;
    public string Emoji { get; init; } = "👀";
    public string Instruction { get; init; } = string.Empty;
    public int Seconds { get; init; }
}

/// <summary>儿童版眼保健操：分节引导，每节配语音提示与倒计时。</summary>
public static class EyeExerciseData
{
    public static readonly List<EyeExerciseStep> Steps = new()
    {
        new() { Name = "闭眼放松", Emoji = "👐", Instruction = "轻轻闭上眼睛，深呼吸，让眼睛安静地休息一下。", Seconds = 10 },
        new() { Name = "按揉睛明穴", Emoji = "👃", Instruction = "用双手食指轻轻按揉鼻梁两侧靠近眼角的穴位，一按一松，慢慢做。", Seconds = 20 },
        new() { Name = "转动眼球", Emoji = "🔄", Instruction = "睁开眼睛，慢慢把眼球向上、向下、向左、向右转动，每个方向停一下。", Seconds = 20 },
        new() { Name = "远眺放松", Emoji = "🌿", Instruction = "看向远处绿色的景物，数到十，让眼睛放松下来。", Seconds = 15 }
    };
}
