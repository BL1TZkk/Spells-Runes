using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using SpellsAndRunes.GUI;
using SpellsAndRunes.Lore;
using SpellsAndRunes.Network;
using SpellsAndRunes;
using SpellsAndRunes.Spells;

namespace SpellsAndRunes.Items;

public class ItemScroll : Item
{
    private string ScrollId
    {
        get
        {
            var path = Code?.Path ?? "";
            path = path.Replace("scroll-", "").Replace("{type}-", "");
            return path;
        }
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel,
        EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (entitySel != null)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
            return;
        }

        handling = EnumHandHandling.PreventDefault;

        if (byEntity.World.Side != EnumAppSide.Client) return;

        var capi = byEntity.World.Api as ICoreClientAPI;
        if (capi == null) return;

        string id = ScrollId;
        var entry = SpellbookLoreRegistry.Get(id);
        if (entry == null) return;

        bool isNew = !(PlayerSpellData.For(capi.World.Player.Entity)?.IsLoreEntryUnlocked(id) ?? false);

        capi.World.PlaySoundAt(
            new AssetLocation("game:sounds/held/bookturn1"),
            capi.World.Player.Entity, null, false, 16f, 0.7f);

        var dialog = new GuiDialogReadScroll(capi, entry);

        if (isNew)
            dialog.OnClosed += () => new GuiHudLoreNotification(capi, entry.Title, entry.Category);

        dialog.TryOpen();

        var mod = capi.ModLoader.GetModSystem<SpellsAndRunesMod>();
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
