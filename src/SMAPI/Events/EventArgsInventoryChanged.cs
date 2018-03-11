using System;
using System.Collections.Generic;
using System.Linq;
using StardewValley;

namespace StardewModdingAPI.Events
{
    /// <summary>Event arguments for a <see cref="PlayerEvents.InventoryChanged"/> event.</summary>
    public class EventArgsInventoryChanged : EventArgs
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The player's inventory.</summary>
#if STARDEW_VALLEY_1_3
        public IList<Item> Inventory { get; }
#else
        public List<Item> Inventory { get; }
#endif

        /// <summary>The added items.</summary>
        public List<ItemStackChange> Added { get; }

        /// <summary>The removed items.</summary>
        public List<ItemStackChange> Removed { get; }

        /// <summary>The items whose stack sizes changed.</summary>
        public List<ItemStackChange> QuantityChanged { get; }


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="inventory">The player's inventory.</param>
        /// <param name="changedItems">The inventory changes.</param>
        public EventArgsInventoryChanged(
#if STARDEW_VALLEY_1_3
            IList<Item> inventory,
#else
            List<Item> inventory,
#endif
            List<ItemStackChange> changedItems)
        {
            this.Inventory = inventory;
            this.Added = changedItems.Where(n => n.ChangeType == ChangeType.Added).ToList();
            this.Removed = changedItems.Where(n => n.ChangeType == ChangeType.Removed).ToList();
            this.QuantityChanged = changedItems.Where(n => n.ChangeType == ChangeType.StackChange).ToList();
        }
    }
}
