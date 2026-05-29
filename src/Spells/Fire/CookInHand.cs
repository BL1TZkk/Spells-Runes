using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SpellsAndRunes.Spells.Fire;

/// <summary>
/// Tier I Enchantment - cooks the item held in hand over a short channel.
/// Requires Hot Skin + Spark.
/// </summary>
public class CookInHand : Spell
{
    private const float BaseCookSeconds = 3.5f;
    private static readonly Dictionary<long, CookState> CookStates = new();

    public override string Id          => "fire_cook_in_hand";
    public override string Name        => "Cook in Hand";
    public override string Description => "Channel fire through your hands, slowly cooking whatever you hold.";

    public override SpellTier    Tier    => SpellTier.Novice;
    public override SpellElement Element => SpellElement.Fire;
    public override SpellType    Type    => SpellType.Enchantment;

    public override float FluxCost => 12f;
    public override float CastTime => 0f;
    public override string? AnimationCode => "fire_cook_in_hand";
    public override bool AnimationTakesOverBody => false;

    public override (int col, int row) TreePosition => (1, 1);

    public override IReadOnlyList<string> Prerequisites => new[] { "fire_hot_skin", "fire_spark" };

    public override void Execute(EntityAgent caster, IWorldAccessor world, int spellLevel)
    {
    }

    public override void OnTick(EntityAgent caster, IWorldAccessor world, float deltaTime, int spellLevel = 1)
    {
        if (world.Side != EnumAppSide.Server || caster is not EntityPlayer entityPlayer) return;
        if (world.PlayerByUid(entityPlayer.PlayerUID) is not IServerPlayer player) return;

        ItemSlot? slot = player.InventoryManager.ActiveHotbarSlot;
        ItemStack? stack = slot?.Itemstack;
        if (slot == null || stack == null)
        {
            CookStates.Remove(caster.EntityId);
            return;
        }

        string signature = GetStackSignature(stack);
        if (!CookStates.TryGetValue(caster.EntityId, out var state) || state.Signature != signature)
        {
            state = new CookState(signature);
            CookStates[caster.EntityId] = state;
        }

        state.Elapsed += deltaTime;
        float cookSeconds = Math.Max(1.4f, BaseCookSeconds - 0.15f * (spellLevel - 1));
        if (state.Elapsed < cookSeconds) return;

        state.Elapsed = 0f;
        if (!TryCookOne(world, player, slot))
        {
            CookStates.Remove(caster.EntityId);
        }
    }

    public override void OnEnd(EntityAgent caster, IWorldAccessor world)
    {
        CookStates.Remove(caster.EntityId);
    }

    private static bool TryCookOne(IWorldAccessor world, IServerPlayer player, ItemSlot inputSlot)
    {
        ItemStack? inputStack = inputSlot.Itemstack;
        if (inputStack == null) return false;

        ItemStack? cooked = TryBakeOne(world, inputStack) ?? TrySmeltOne(world, inputStack);
        if (cooked == null) return false;

        int consumeQuantity = Math.Max(1, inputStack.Collectible.GetCombustibleProperties(world, inputStack, null)?.SmeltedRatio ?? 1);
        inputSlot.TakeOut(consumeQuantity);
        inputSlot.MarkDirty();

        if (inputSlot.Empty)
        {
            inputSlot.Itemstack = cooked;
            inputSlot.MarkDirty();
            player.InventoryManager.BroadcastHotbarSlot();
        }
        else if (!player.InventoryManager.TryGiveItemstack(cooked, false))
        {
            world.SpawnItemEntity(cooked, player.Entity.Pos.XYZ.Add(0, player.Entity.LocalEyePos.Y * 0.5, 0));
        }

        world.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), player.Entity.Pos.X, player.Entity.Pos.Y, player.Entity.Pos.Z, player, false, 12f, 0.65f);
        return true;
    }

    private static ItemStack? TryBakeOne(IWorldAccessor world, ItemStack inputStack)
    {
        BakingProperties? baking = BakingProperties.ReadFrom(inputStack);
        if (baking?.ResultCode == null) return null;

        var resultCode = new AssetLocation(baking.ResultCode);
        CollectibleObject? collectible = inputStack.Class == EnumItemClass.Block
            ? world.GetBlock(resultCode)
            : world.GetItem(resultCode);
        if (collectible == null) return null;

        var result = new ItemStack(collectible, 1);
        TransitionableProperties? perishProps = result.Collectible.GetTransitionableProperties(world, result, null)
            ?.FirstOrDefault(p => p.Type == EnumTransitionType.Perish);
        if (perishProps != null)
        {
            CollectibleObject.CarryOverFreshness(world.Api, new DummySlot(inputStack), result, perishProps);
        }

        inputStack.Collectible.GetCollectibleInterface<IBakeableCallback>()?.OnBaked(inputStack, result);
        return result;
    }

    private static ItemStack? TrySmeltOne(IWorldAccessor world, ItemStack inputStack)
    {
        CombustibleProperties? props = inputStack.Collectible.GetCombustibleProperties(world, inputStack, null);
        if (props?.SmeltedStack?.ResolvedItemstack == null) return null;
        if (props.SmeltingType != EnumSmeltType.Cook && props.SmeltingType != EnumSmeltType.Bake) return null;
        if (inputStack.StackSize < Math.Max(1, props.SmeltedRatio)) return null;

        ItemStack singleStack = inputStack.Clone();
        singleStack.StackSize = Math.Max(1, props.SmeltedRatio);
        var singleInput = new DummySlot(singleStack);
        var output = new DummySlot();

        if (!inputStack.Collectible.CanSmelt(world, null, singleStack, null)) return null;
        inputStack.Collectible.DoSmelt(world, null, singleInput, output);

        return output.Itemstack;
    }

    private static string GetStackSignature(ItemStack stack)
    {
        return $"{stack.Class}:{stack.Collectible.Code}:{stack.Attributes?.ToJsonToken()}";
    }

    private sealed class CookState
    {
        public CookState(string signature)
        {
            Signature = signature;
        }

        public string Signature { get; }
        public float Elapsed { get; set; }
    }

    public static void SpawnFx(IWorldAccessor world, Vec3d origin, Vec3d lookDir, int spellLevel)
    {
        var rng = world.Rand;
        Vec3d up = new Vec3d(0, 1, 0);
        int mult = 1 + (spellLevel - 1) / 4;
        var flameCenter = origin + lookDir * 0.42 + up * 0.48;

        for (int i = 0; i < 14 * mult; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2;
            double r = rng.NextDouble() * 0.11;
            var pos = flameCenter.AddCopy(Math.Cos(a) * r, rng.NextDouble() * 0.08, Math.Sin(a) * r);

            world.SpawnParticles(new SimpleParticleProperties
            {
                MinQuantity = 1,
                AddQuantity = 0,
                MinPos = pos,
                AddPos = new Vec3d(0.025, 0.035, 0.025),
                MinVelocity = new Vec3f((float)(lookDir.X * 0.05), 0.28f, (float)(lookDir.Z * 0.05)),
                AddVelocity = new Vec3f(0.08f, 0.16f, 0.08f),
                LifeLength = 0.22f + (float)rng.NextDouble() * 0.14f,
                MinSize = 0.08f,
                MaxSize = 0.18f,
                GravityEffect = -0.08f,
                Color = ColorUtil.ColorFromRgba(5 + rng.Next(45), 145 + rng.Next(80), 255, 230),
                ParticleModel = EnumParticleModel.Quad,
                WithTerrainCollision = false,
                ShouldDieInLiquid = true
            });
        }
    }
}
