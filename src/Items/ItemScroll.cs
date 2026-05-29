using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using SpellsAndRunes.GUI;
using SpellsAndRunes.Lore;
using SpellsAndRunes.Network;

namespace SpellsAndRunes.Items;

public class ItemScroll : Item
{
    private string ScrollId => Code?.Path?.Replace("scroll-", "") ?? "";

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (blockSel != null || entitySel != null)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        handling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side != EnumAppSide.Client) return;

        var capi = api as ICoreClientAPI;
        if (capi == null) return;

        string id = ScrollId;
        var entry = ScrollRegistry.Get(id);
        if (entry == null) return;

        var dialog = new GuiDialogReadScroll(capi, entry);
        dialog.TryOpen();

        var mod = api.ModLoader.GetModSystem<SpellsAndRunesMod>();
        mod?.SendReadScroll(id);
    }

    public override WorldInteraction[] GetHeldInteractionHelp(ItemSlot inSlot)
    {
        return new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "heldhelp-read",
                MouseButton = EnumMouseButton.Right
            }
        };
    }
}
