using SPT.Common.Http;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SPT.Reflection.Utils;
using System.Threading.Tasks;

namespace LootValuePlus
{
	internal static class FleaPriceCache
	{
		static Dictionary<string, CachePrice> cache = new Dictionary<string, CachePrice>();
		public static ISession Session => ClientAppUtils.GetMainApp().GetClientBackEndSession();

		public static async Task<int?> FetchPrice(string templateId)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;

			if (!fleaAvailable)
				return null;

			if (cache.ContainsKey(templateId))
			{
				double secondsSinceLastUpdate = (DateTime.Now - cache[templateId].lastUpdate).TotalSeconds;
				if (secondsSinceLastUpdate > 300)
					return await QueryAndTryUpsertPrice(templateId, true);
				else
					return cache[templateId].price;
			}
			else
				return await QueryAndTryUpsertPrice(templateId, false);
		}

		private static async Task<string> QueryPrice(string templateId)
		{
			return await CustomRequestHandler.PostJsonAsync("/LootValue/GetItemLowestFleaPrice", JsonConvert.SerializeObject(new FleaPriceRequest(templateId)));
		}

		private static async Task<int?> QueryAndTryUpsertPrice(string templateId, bool update)
		{
			string response = await QueryPrice(templateId);
			bool hasPlayerFleaPrice = !(string.IsNullOrEmpty(response) || response == "null");

			int price;
			if (hasPlayerFleaPrice)
			{
				price = int.Parse(response);
			}
			else
			{
				price = 0;
			}

			if (update)
				cache[templateId].Update(price);
			else
				cache[templateId] = new CachePrice(price);

			return price;
		}
	}

	internal class CachePrice
	{
		public int price { get; private set; }
		public DateTime lastUpdate { get; private set; }

		public CachePrice(int price)
		{
			this.price = price;
			lastUpdate = DateTime.Now;
		}

		public void Update(int price)
		{
			this.price = price;
			lastUpdate = DateTime.Now;
		}
	}
}
