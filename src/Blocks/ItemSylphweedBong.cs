using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using SpellsAndRunes.Spells;

namespace SpellsAndRunes.Blocks;

public class ItemSylphweedBong : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        // Delegate placement/targeting path to base so GroundStorable behavior can run.
        if (byEntity.Controls.ShiftKey || blockSel != null || entitySel != null)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        handling = EnumHandHandling.PreventDefault;      
    }
    public override bool OnHeldInteractStep(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
    BlockSelection blockSel, EntitySelection entitySel)
    {
        if (byEntity.Controls.ShiftKey || blockSel != null || entitySel != null) return false;

        return secondsUsed < 2.5f;
    }
    public override void OnHeldInteractStop(float secondsUsed, ItemSlot slot, EntityAgent byEntity,
    BlockSelection blockSel, EntitySelection entitySel)
    {
        if (secondsUsed < 2.5f) return;


        if (byEntity is not EntityPlayer playerEntity) return;

        var player = playerEntity.Player as IServerPlayer;
        if (player == null) return;

        var data = PlayerSpellData.For(byEntity);
        if (data.IsFluxUnlocked)
        {
            player.SendMessage(0, Lang.Get("spellsandrunes:message-flux-already-unlocked"), EnumChatType.Notification);
            return;
        }

        // Find grounded sylphweed in hotbar
        var inv = player.InventoryManager.GetHotbarInventory();
        ItemSlot? groundSlot = null;
        for (int i = 0; i < inv.Count; i++)
        {
            var s = inv[i];
            if (s.Itemstack?.Collectible?.Code?.Path == "sylphweed-grounded")
            { groundSlot = s; break; }
        }

        if (groundSlot == null)
        {
            player.SendMessage(0, Lang.Get("spellsandrunes:message-sylphweed-ground-required"), EnumChatType.Notification);
            return;
        }

        // Consume one ground sylphweed
        groundSlot.TakeOut(1);
        groundSlot.MarkDirty();

        // Unlock flux and first row of Air spells
        data.UnlockFlux();
        data.Unlock("air_feather_fall");
        data.Unlock("air_air_push");
        data.UnlockLoreEntry("journal-air-1");
        player.SendMessage(0, Lang.Get("spellsandrunes:message-flux-awakens"), EnumChatType.Notification);

    }
}
