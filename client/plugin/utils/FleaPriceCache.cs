using SPT.Common.Http;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SPT.Reflection.Utils;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Linq;

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
			{
				return null;
			}

			if (cache.ContainsKey(templateId))
			{
				var cachedPrice = cache[templateId];
				if(cachedPrice.ShouldUpdate()) {
					var price = await QueryTemplateIdSellingPrice(templateId);
					cachedPrice.Update(price);
				}
				return cachedPrice.price;
			}
			else
			{
				var price = await QueryTemplateIdSellingPrice(templateId);
				cache[templateId] = new CachePrice(price);
				return price;
			}
		}

		public static async Task<int?> FetchPrice(IEnumerable<string> templateIds)
		{
			bool fleaAvailable = Session.RagFair.Available || LootValueMod.ShowFleaPriceBeforeAccess.Value;

			if (!fleaAvailable)
			{
				return null;
			}

			var templateIdsInCache = templateIds.Where(cache.ContainsKey);
			// Globals.logger.LogInfo($"Templates in cache: {templateIdsInCache.ToJson()}");

			var templateIdsNotInCache = templateIds.Where(id => !cache.ContainsKey(id));
			// Globals.logger.LogInfo($"Templates not in cache: {templateIdsNotInCache.ToJson()}");


			var templateIdsThatMustBeUpdated = templateIdsInCache.Where(id =>
			{
				var cachedPrice = cache[id];
				return cachedPrice.ShouldUpdate();
			});
			var templateIdsToFetch = templateIdsThatMustBeUpdated.Concat(templateIdsNotInCache);
			// Globals.logger.LogInfo($"Template ids to fetch: {templateIdsToFetch.ToJson()}");

			// fetch all ids & update cache
			var prices = await QueryTemplateIdSellingPrice(templateIdsToFetch);
			prices.ExecuteForEach(price => 
			{
				var templateId = price.templateId;
				if (cache.ContainsKey(templateId))  {
					cache[templateId].Update(price.price);
				} else {
					cache[templateId] = new CachePrice(price.price);
				}
			});

			return templateIds.Select(id => cache[id].price).Sum();
		}

		private static async Task<int> QueryTemplateIdSellingPrice(string templateId) 
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

			return price;
		}

		private static async Task<IEnumerable<FleaPrice>> QueryTemplateIdSellingPrice(IEnumerable<string> templateIds) 
		{
			if(templateIds.IsNullOrEmpty()) {
				return [];
			}

			string response = await QueryPrice(templateIds);
			// Globals.logger.LogInfo($"RESPONSE: {response}");
			return JsonConvert.DeserializeObject<FleaPricesResponse>(response).prices;
		}

		private static async Task<string> QueryPrice(string templateId)
		{
			return await CustomRequestHandler.PostJsonAsync("/LootValue/GetItemLowestFleaPrice", JsonConvert.SerializeObject(new FleaPriceRequest(templateId)));
		}

		private static async Task<string> QueryPrice(IEnumerable<string> templateIds)
		{
			return await CustomRequestHandler.PostJsonAsync("/LootValue/GetMultipleItemsSellingFleaPrice", JsonConvert.SerializeObject(new FleaPricesRequest(templateIds)));
		}

	}

	public class FleaPriceRequest
	{
		public string templateId;
		public FleaPriceRequest(string templateId) => this.templateId = templateId;
	}

	public class FleaPricesRequest
	{
		public IEnumerable<string> templateIds;
		public FleaPricesRequest(IEnumerable<string> templateIds) => this.templateIds = templateIds;
	}

	public class FleaPrice
	{
		public string templateId {get; set;}
		public int price {get; set;}
	}

	public class FleaPricesResponse
	{
		public Collection<FleaPrice> prices {get; set;}
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

		public bool ShouldUpdate() 
		{
			return (DateTime.Now - lastUpdate).TotalSeconds >= 300;
		}
	}
}
