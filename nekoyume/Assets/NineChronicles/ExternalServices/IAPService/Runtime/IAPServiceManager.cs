#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Libplanet;
using NineChronicles.ExternalServices.IAPService.Runtime.Models;
using NineChronicles.ExternalServices.IAPService.Runtime.Responses;
using UnityEngine;

namespace NineChronicles.ExternalServices.IAPService.Runtime
{
    public class IAPServiceManager : IDisposable
    {
        public static readonly TimeSpan DefaultProductsCacheLifetime =
            TimeSpan.FromMinutes(10);

        private readonly IAPServiceClient _client;
        private readonly IAPServicePoller _poller;
        private readonly IAPServiceCache _cache;
        private readonly Store _store;

        public bool IsInitialized { get; private set; }
        public bool IsDisposed { get; private set; }

        public IAPServiceManager(string url, Store store)
        {
            _client = new IAPServiceClient(url);
            _poller = new IAPServicePoller(_client);
            _poller.OnPoll += OnPoll;
            _cache = new IAPServiceCache(DefaultProductsCacheLifetime);
            _store = store;
        }

        public async Task InitializeAsync(TimeSpan? productsCacheLifetime = null)
        {
            if (IsInitialized)
            {
                Debug.LogError("IAPServiceManager is already initialized.");
                return;
            }

            var (code, error, _, _) = await _client.PingAsync();
            if (code != HttpStatusCode.OK ||
                !string.IsNullOrEmpty(error))
            {
                Debug.LogError(
                    $"Failed to initialize IAPServiceManager: {code}, {error}");
                return;
            }

            if (productsCacheLifetime is not null)
            {
                _cache.SetOptions(productsCacheLifetime);
            }

            IsInitialized = true;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                Debug.LogError("IAPServiceManager is already disposed.");
                return;
            }

            _client.Dispose();
            _poller.OnPoll -= OnPoll;
            _poller.Clear();
            IsDisposed = true;
        }

        // public async Task CheckReceiptsAsync()
        // {
        //     if (Application.platform == RuntimePlatform.Android)
        //     {
        //     }
        //     // get all receipts from stores.
        //
        //     // get all receipts from IAPService.
        //
        //     //request to purchase to IAPService if there are missing receipts.
        // }

        public async Task<IReadOnlyList<ProductSchema>?> GetProductsAsync(
            Address agentAddr,
            bool force = false)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("IAPServiceManager is not initialized.");
                return null;
            }

            if (!force && _cache.Products is not null)
            {
                return _cache.Products;
            }

            var (code, error, mediaType, content) = await _client.ProductAsync(agentAddr);
            if (code != HttpStatusCode.OK ||
                !string.IsNullOrEmpty(error))
            {
                Debug.LogError(
                    $"FetchProducts failed: {code}, {error}, {mediaType}, {content}");
                return null;
            }

            if (mediaType != "application/json")
            {
                Debug.LogError(
                    $"Unexpected media type: {code}, {error}, {mediaType}, {content}");
                return null;
            }

            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError(
                    $"Content is empty: {code}, {error}, {mediaType}, {content}");
                return null;
            }

            try
            {
                _cache.Products = JsonSerializer.Deserialize<ProductSchema[]>(
                    content!,
                    IAPServiceClient.JsonSerializerOptions)!;
                return _cache.Products;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        public async Task<PurchaseRequestResponse200?> PurchaseRequestAsync(
            string receipt,
            Address agentAddr,
            Address avatarAddr)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("IAPServiceManager is not initialized.");
                return null;
            }

            var (code, error, mediaType, content) =
                await _client.PurchaseRequestAsync(
                    _store,
                    receipt,
                    agentAddr,
                    avatarAddr);
            if (code != HttpStatusCode.OK ||
                !string.IsNullOrEmpty(error))
            {
                Debug.LogError(
                    $"Purchase failed: {code}, {error}, {mediaType}, {content}");
                return null;
            }

            if (mediaType != "application/json")
            {
                Debug.LogError(
                    $"Unexpected media type: {code}, {error}, {mediaType}, {content}");
                return null;
            }

            if (string.IsNullOrEmpty(content))
            {
                Debug.LogError(
                    $"Content is empty: {code}, {error}, {mediaType}, {content}");
                return null;
            }

            try
            {
                var result = JsonSerializer.Deserialize<ReceiptDetailSchema>(
                    content!,
                    IAPServiceClient.JsonSerializerOptions)!;
                _cache.PurchaseProcessResults[result.Uuid] = result;
                if (result.Status == ReceiptStatus.Invalid ||
                    result.Status == ReceiptStatus.Unknown)
                {
                    UnregisterAndCache(result);
                }
                else
                {
                    _poller.Register(result.Uuid);
                }

                return new PurchaseRequestResponse200 { Content = result };
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
        }

        private void UnregisterAndCache(ReceiptDetailSchema result)
        {
            _poller.Unregister(result.Uuid);
            _cache.PurchaseProcessResults[result.Uuid] = result;
        }

        private void OnPoll(ReceiptDetailSchema[] results)
        {
            foreach (var result in results)
            {
                switch (result.Status)
                {
                    case ReceiptStatus.Init:
                    case ReceiptStatus.ValidationRequest:
                        continue;
                    case ReceiptStatus.Valid:
                        break;
                    case ReceiptStatus.Invalid:
                        Debug.LogWarning($"Invalid receipt: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    case ReceiptStatus.Unknown:
                        Debug.LogWarning($"Unknown receipt: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                switch (result.TxStatus)
                {
                    case TxStatus.Created:
                    case TxStatus.Staged:
                        continue;
                    case TxStatus.Success:
                        Debug.LogWarning($"Tx success: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    case TxStatus.Failure:
                        Debug.LogWarning($"Tx failure: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    case TxStatus.Invalid:
                        Debug.LogWarning($"Tx invalid: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    case TxStatus.NotFound:
                        Debug.LogWarning($"Tx not found: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    case TxStatus.FailToCreate:
                        Debug.LogWarning($"Tx failed to create: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    case TxStatus.Unknown:
                        Debug.LogWarning($"Tx unknown: {result.Uuid}");
                        UnregisterAndCache(result);
                        continue;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
