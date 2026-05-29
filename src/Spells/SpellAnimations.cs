using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace SpellsAndRunes.Spells;

/// <summary>Client-side helper for playing spell animations on player entities.</summary>
public static class SpellAnimations
{
    // Leg bones that should stay controlled by default movement for partial-body spell animations.
    private static readonly string[] LegBones =
    {
        "LowerTorso",
        "UpperFootL", "LowerFootL", "FootAttachmentL",
        "UpperFootR", "LowerFootR", "FootAttachmentR",
    };

    /// <summary>Play a spell animation on the given entity.</summary>
    public static void Play(EntityAgent entity, string animCode, bool takesOverBody = false, float animationSpeed = 1f)
    {
        bool snapSpark = animCode == "fire_spark";
        var meta = new AnimationMetaData
        {
            Code             = animCode,
            Animation        = animCode,
            BlendMode        = takesOverBody ? EnumAnimationBlendMode.AddAverage : EnumAnimationBlendMode.Average,
            EaseInSpeed      = 10f,
            EaseOutSpeed     = snapSpark ? 999f : 10f,
            Weight           = takesOverBody ? 1000f : 1000f,
            AnimationSpeed   = animationSpeed,
            SupressDefaultAnimation = takesOverBody,
        };

        if (!takesOverBody)
        {
            meta.ElementWeight = new Dictionary<string, float>();
            foreach (var bone in LegBones)
                meta.ElementWeight[bone] = 0f;
        }

        entity.AnimManager?.StartAnimation(meta);
    }

    /// <summary>Stop a spell animation by code.</summary>
    public static void Stop(EntityAgent entity, string animCode)
    {
        entity.AnimManager?.StopAnimation(animCode);
    }
}
