using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using SPT.Reflection.Utils;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
// fix VVVVVVV
using CurrencyUtil = GClass2934;
using FleaRequirement = GClass2102;

namespace LootValuePlus
{

	internal static class FleaUtils
	{

		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

		public static bool HasFleaSlotToSell()
		{
			return LootValueMod.IgnoreFleaMaxOfferCount.Value || Session.RagFair.MyOffersCount < Session.RagFair.GetMaxOffersCount(Session.RagFair.MyRating);
		}

		public static int GetFleaValue(IEnumerable<Item> items)
		{
			var soldableItems = items.Where(item => item.Template.CanSellOnRagfair);
			var templateIds = soldableItems.Select(item => item.TemplateId.ToString());

			var price = Task.Run(() => FleaPriceCache.FetchPrice(templateIds)).Result;

			if (!price.HasValue)
			{
				return 0;
			}

			return price.Value;
		}

		public static int GetFleaValue(Item item)
		{

			if (!item.Template.CanSellOnRagfair)
			{
				return 0;
			}

			var price = Task.Run(() => FleaPriceCache.FetchPrice(item.TemplateId)).Result;

			if (!price.HasValue)
			{
				return 0;
			}

			return (int)price.Value;
		}

		public static int GetFleaMarketUnitPrice(Item item)
		{
			if (!item.Template.CanSellOnRagfair)
			{
				return 0;
			}

			int unitPrice = GetFleaValue(item);
			return unitPrice;
		}

		public static int GetFleaMarketUnitPriceWithModifiers(Item item)
		{
			int price = GetFleaMarketUnitPrice(item);

			bool applyConditionReduction = LootValueMod.ReducePriceInFleaForBrokenItem.Value;
			if (applyConditionReduction)
			{
				price = (int)(price * ItemUtils.GetResourcePercentageOfItem(item));
			}

			return price;
		}

		public static int GetTotalPriceOfAllSimilarItemsWithinSameContainer(Item item)
		{
			var includePinned = LootValueMod.AllowQuickSellPinned.Value;
			var includeLocked = LootValueMod.AllowQuickSellLocked.Value;
			var unitPrice = GetFleaMarketUnitPriceWithModifiers(item);
			var items = ItemUtils.GetItemsSimilarToItemWithinSameContainer(item, includePinned, includeLocked);
			return items.Select(i => unitPrice * i.StackObjectsCount).Sum();
		}

		public static bool IsItemFleaMarketPriceBelow(Item item, int priceThreshold, bool considerMultipleItems = false)
		{
			var unitPrice = GetFleaMarketUnitPriceWithModifiers(item);
			if (considerMultipleItems)
			{
				var includePinned = LootValueMod.AllowQuickSellPinned.Value;
				var includeLocked = LootValueMod.AllowQuickSellLocked.Value;
				var items = ItemUtils.GetItemsSimilarToItemWithinSameContainer(item, includePinned, includeLocked);
				var price = items.Select(i => unitPrice * i.StackObjectsCount).Sum();
				return price < priceThreshold;
			}
			else
			{
				var price = unitPrice * item.StackObjectsCount;
				return price < priceThreshold;
			}
		}


		public static bool ContainsNonFleableItemsInside(Item item)
		{
			return item.GetAllItems().Any(i => i.Template.CanSellOnRagfair == false);
		}

		public static bool CanBeSoldInFleaRightNow(Item item, bool displayWarning = true)
		{

			if (!Session.RagFair.Available)
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Flea market is not available yet.");

				return false;
			}

			// we need to check if the base item is sellable
			if (!item.Template.CanSellOnRagfair)
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item is banned from flea market.");

				return false;
			}

			if (!HasFleaSlotToSell())
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Maximum number of flea offers reached.");

				return false;
			}

			if (item.IsNotEmpty())
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item is not empty.");

				return false;

			}

			if (ContainsNonFleableItemsInside(item))
			{

				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item contains banned fleamarket items.");

				return false;
			}

			// fallback as any other reason will get caught here
			if (!item.CanSellOnRagfair)
			{
				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item can't be sold right now.");

				return false;
			}

			var canSellPinnedItems = LootValueMod.AllowQuickSellPinned.Value;
			var canSellLockedItems = LootValueMod.AllowQuickSellLocked.Value;

			if (item.PinLockState == EItemPinLockState.Pinned && !canSellPinnedItems)
			{
				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item is pinned.");

				return false;
			}

			if (item.PinLockState == EItemPinLockState.Locked && !canSellLockedItems)
			{
				if (displayWarning)
					NotificationManagerClass.DisplayWarningNotification("Quicksell: Item is locked.");

				return false;
			}

			return true;
		}

		public static bool CanSellMultipleOfItem(Item item)
		{

			bool sellMultipleEnabled = LootValueMod.SellSimilarItems.Value;
			bool sellMultipleOnlyFiR = LootValueMod.SellOnlySimilarItemsFiR.Value;
			bool isItemFindInRaid = item.MarkedAsSpawnedInSession;

			if (!sellMultipleEnabled)
			{
				return false;
			}


			if (sellMultipleOnlyFiR && !isItemFindInRaid)
			{
				return false;
			}

			return true;
		}


		public static void SellFleaItemOrMultipleItemsIfEnabled(Item item)
		{
			if (!CanSellMultipleOfItem(item))
			{
				SellToFlea(item);
				return;
			}

			var includePinned = LootValueMod.AllowQuickSellPinned.Value;
			var includeLocked = LootValueMod.AllowQuickSellLocked.Value;
			var similarBundledItems = ItemUtils.GetItemsSimilarToItemWithinSameContainer(item, includePinned, includeLocked);
			SellToFlea(item, similarBundledItems);
		}

		public static void SellToFlea(Item itemToCheck, IEnumerable<Item> itemsToSell)
		{
			if (!CanBeSoldInFleaRightNow(itemToCheck))
			{
				return;
			}

			var price = GetFleaMarketUnitPriceWithModifiers(itemToCheck);
			var ids = itemsToSell.Select(i => i.Id).ToArray();

			ApplyFleaOffer(price, ids);
		}

		public static void SellToFlea(Item itemToSell)
		{
			if (!CanBeSoldInFleaRightNow(itemToSell))
			{
				return;
			}

			var price = GetFleaMarketUnitPriceWithModifiers(itemToSell);
			var ids = new string[1] { itemToSell.Id };

			ApplyFleaOffer(price, ids);
		}

		private static void ApplyFleaOffer(int price, string[] itemIds)
		{
			var offerRequeriment = new FleaRequirement()
			{
				count = price, //undercut by 1 ruble
				_tpl = "5449016a4bdc2d6f028b456f" //id of ruble
			};

			Globals.logger.LogInfo($"Invoking Session.RagFair.AddOffer(false, '{itemIds.EnumerableToString(", ")}', ({offerRequeriment.ToPrettyJson()}), null)");
			Session.RagFair.AddOffer(false, itemIds, [offerRequeriment], null);
		}


	}

	internal static class TraderUtils
	{

		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

		public static int GetBestTraderPrice(Item item)
		{
			var offer = GetBestTraderOffer(item);
			if (offer == null)
			{
				return 0;
			}
			return offer.Price;
		}

		public static TraderOffer GetBestTraderOffer(Item item)
		{
			if (!Session.Profile.Examined(item))
			{
				return null;
			}

			var clone = ItemUtils.CloneItemSafely(item);

			var bestOffer =
				Session.Traders
					.Where(trader => trader.Info.Available && !trader.Info.Disabled && trader.Info.Unlocked && !trader.Settings.AvailableInRaid)
					.Select(trader => GetTraderOffer(clone, trader))
					.Where(offer => offer != null)
					.OrderByDescending(offer => offer.Price)
					.FirstOrDefault();

			return bestOffer;
		}

		private static TraderOffer GetTraderOffer(Item item, TraderClass trader)
		{
			var result = trader.GetUserItemPrice(item);

			if (result == null)
			{
				return null;
			}

			// TODO: try to see if we can convert non rubles to rubles

			return result.HasValue ? new TraderOffer(
				trader.Id,
				trader.LocalizedName,
				result.Value.Amount,
				CurrencyUtil.GetCurrencyCharById(result.Value.CurrencyId.Value),
				trader.GetSupplyData().CurrencyCourses[result.Value.CurrencyId.Value],
				item.StackObjectsCount
			) : null;
		}

		public static bool ShouldSellToTraderDueToPriceOrCondition(Item item)
		{
			var flags = DurabilityOrProfitConditionFlags.GetDurabilityOrProfitConditionFlagsForItem(item);
			return flags.ShouldSellItemToTraderDueToTriggeredConditions();
		}

		public static void SellToTrader(Item item)
		{
			try
			{
				TraderOffer bestTraderOffer = GetBestTraderOffer(item);

				if (bestTraderOffer == null)
				{
					NotificationManagerClass.DisplayWarningNotification("Quicksell Error: No trader will purchase this item.");
					return;
				}

				if (item.IsNotEmpty())
				{
					NotificationManagerClass.DisplayWarningNotification("Quicksell: item is not empty.");
					return;
				}

				SellToTrader(item, bestTraderOffer);
			}
			catch (Exception ex)
			{
				Globals.logger.LogInfo($"Something fucked up: {ex.Message}");
				Globals.logger.LogInfo($"{ex.InnerException.Message}");
			}
		}

		public static void SellToTrader(Item item, TraderOffer bestTraderOffer)
		{
			TraderClass tc = Session.GetTrader(bestTraderOffer.TraderId);

			GClass2332.Class1936 @class = new GClass2332.Class1936();
			@class.source = new TaskCompletionSource<bool>();

			var itemRef = new EFT.Trading.TradingItemReference
			{
				Item = item,
				Count = item.StackObjectsCount
			};

			Session.ConfirmSell(tc.Id, new EFT.Trading.TradingItemReference[1] { itemRef }, bestTraderOffer.Price, new Callback(@class.method_0));
			Singleton<GUISounds>.Instance.PlayUISound(EUISoundType.TradeOperationComplete);
		}


	}


	public sealed class TraderOffer
	{
		public string TraderId;
		public string TraderName;
		public int Price;
		public string Currency;
		public double Course;
		public int Count;

		public TraderOffer(string traderId, string traderName, int price, string currency, double course, int count)
		{
			TraderId = traderId;
			TraderName = traderName;
			Price = price;
			Currency = currency;
			Course = course;
			Count = count;
		}
	}


	internal class DurabilityOrProfitConditionFlags
	{

		public bool shouldSellToTraderDueToBeingNonOperational;
		public bool shouldSellNonOperationalToFleaDueToHighProfit;
		public bool shouldSellToTraderBecauseDurabilityIsTooLow;
		public bool shouldSellToTraderBecauseFleaProfitIsTooLow;


		public DurabilityOrProfitConditionFlags(
			bool shouldSellToTraderDueToBeingNonOperational,
			bool shouldSellNonOperationalToFleaDueToHighProfit,
			bool shouldSellToTraderBecauseDurabilityIsTooLow,
			bool shouldSellToTraderBecauseFleaProfitIsTooLow
		)
		{
			this.shouldSellToTraderDueToBeingNonOperational = shouldSellToTraderDueToBeingNonOperational;
			this.shouldSellNonOperationalToFleaDueToHighProfit = shouldSellNonOperationalToFleaDueToHighProfit;
			this.shouldSellToTraderBecauseDurabilityIsTooLow = shouldSellToTraderBecauseDurabilityIsTooLow;
			this.shouldSellToTraderBecauseFleaProfitIsTooLow = shouldSellToTraderBecauseFleaProfitIsTooLow;
		}

		public static DurabilityOrProfitConditionFlags GetDurabilityOrProfitConditionFlagsForItem(Item item)
		{
			bool sellNonOperationalWeaponsToTraderEnabled = LootValueMod.SellToTraderIfWeaponIsNonOperational.Value;
			int nonOperationalFleaValueThreshold = LootValueMod.SellToFleaIfWeaponIsNonOperationalAboveThreshold.Value;
			bool shouldSellToTraderDueToBeingNonOperational = ItemUtils.IsWeaponNonOperational(item) && sellNonOperationalWeaponsToTraderEnabled;
			bool shouldSellNonOperationalToFleaDueToHighProfit = nonOperationalFleaValueThreshold > 0 && !FleaUtils.IsItemFleaMarketPriceBelow(item, nonOperationalFleaValueThreshold, false);

			bool sellTooLowDurabilityItemsToTraderEnabled = LootValueMod.SellToTraderBelowDurabilityThresholdEnabled.Value;
			int durabilityThreshold = LootValueMod.SellToTraderDurabilityThreshold.Value;
			bool shouldSellToTraderBecauseDurabilityIsTooLow = ItemUtils.IsItemBelowDurability(item, durabilityThreshold) && sellTooLowDurabilityItemsToTraderEnabled;

			bool sellTooCheapFleaItemsToTraderEnabled = LootValueMod.SellToTraderBelowProfitThresholdEnabled.Value;
			int fleaMarketProfitThreshold = LootValueMod.SellToTraderProfitThreshold.Value;
			bool shouldSellToTraderBecauseFleaProfitIsTooLow = FleaUtils.IsItemFleaMarketPriceBelow(item, fleaMarketProfitThreshold, FleaUtils.CanSellMultipleOfItem(item)) && sellTooCheapFleaItemsToTraderEnabled;


			return new DurabilityOrProfitConditionFlags(shouldSellToTraderDueToBeingNonOperational, shouldSellNonOperationalToFleaDueToHighProfit, shouldSellToTraderBecauseDurabilityIsTooLow, shouldSellToTraderBecauseFleaProfitIsTooLow);
		}

		public bool ShouldSellDueToBeingWeaponAndNonOperational()
		{
			if (this.shouldSellToTraderDueToBeingNonOperational && !this.shouldSellNonOperationalToFleaDueToHighProfit)
			{
				return true;
			}
			return false;
		}

		public bool IsBelowDurabilityThresholdOrBelowFleaProfitThreshold()
		{
			return this.shouldSellToTraderBecauseDurabilityIsTooLow || this.shouldSellToTraderBecauseFleaProfitIsTooLow;
		}

		public bool ShouldSellItemToTraderDueToTriggeredConditions()
		{

			return this.IsBelowDurabilityThresholdOrBelowFleaProfitThreshold() || this.ShouldSellDueToBeingWeaponAndNonOperational();
		}

	}

}