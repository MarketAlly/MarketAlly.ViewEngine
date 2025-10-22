using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using UnifiedData.Maui;
using UnifiedData.Maui.Core;
using UnifiedData.Maui.Embeddings;

namespace Slyv.Maui.Services
{
    /// <summary>
    /// Wrapper for IUnifiedDataService that respects user settings for vector search
    /// and dynamically updates vector dimensions based on embedding provider
    /// </summary>
    public class SettingsAwareDataService : IUnifiedDataService
    {
        private readonly IUnifiedDataService _innerService;
        private readonly IAppSettingsService _settingsService;
        private readonly IOptions<UnifiedDataOptions> _dataOptions;
        private readonly IEmbeddingProvider _embeddingProvider;

        public SettingsAwareDataService(
            IUnifiedDataService innerService,
            IAppSettingsService settingsService,
            IOptions<UnifiedDataOptions> dataOptions,
            IEmbeddingProvider embeddingProvider)
        {
            _innerService = innerService;
            _settingsService = settingsService;
            _dataOptions = dataOptions;
            _embeddingProvider = embeddingProvider;
        }

        /// <summary>
        /// Sync vector dimensions with current embedding provider
        /// </summary>
        private void SyncVectorDimensions()
        {
            try
            {
                var currentDims = _embeddingProvider.GetDimensions();
                if (_dataOptions.Value.VectorDimensions != currentDims)
                {
                    System.Diagnostics.Debug.WriteLine($"Updating VectorDimensions from {_dataOptions.Value.VectorDimensions} to {currentDims} to match embedding provider");
                    _dataOptions.Value.VectorDimensions = currentDims;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to sync vector dimensions: {ex.Message}");
            }
        }

        // Override SaveAsync to check settings before generating embeddings
        public async Task<string> SaveAsync(UnifiedDocument document)
        {
            // Load current settings
            await _settingsService.LoadSettingsAsync();

            // Sync vector dimensions with current embedding provider
            SyncVectorDimensions();

            // If vector search is disabled in settings, clear any embedding before saving
            if (!_settingsService.EnableVectorSearch)
            {
                document.Embedding = null;
            }

            return await _innerService.SaveAsync(document);
        }

        public async Task<List<string>> SaveBatchAsync(IEnumerable<UnifiedDocument> documents)
        {
            await _settingsService.LoadSettingsAsync();

            // Sync vector dimensions with current embedding provider
            SyncVectorDimensions();

            if (!_settingsService.EnableVectorSearch)
            {
                foreach (var doc in documents)
                {
                    doc.Embedding = null;
                }
            }

            return await _innerService.SaveBatchAsync(documents);
        }

        // Search methods that respect settings
        public async Task<List<SearchResult>> SearchAsync(string query, int maxResults = 10, string? userId = null, int contentType = 0)
        {
            await _settingsService.LoadSettingsAsync();

            // If vector search is disabled, force keyword search
            if (!_settingsService.EnableVectorSearch)
            {
                return await _innerService.KeywordSearchAsync(query, maxResults);
            }

            return await _innerService.SearchAsync(query, maxResults, userId, contentType);
        }

        public async Task<List<SearchResult>> SearchAsync(string query, SearchOptions options)
        {
            await _settingsService.LoadSettingsAsync();

            // Override to disable vector search if settings say so
            if (!_settingsService.EnableVectorSearch)
            {
                options.UseHybrid = false;
            }

            return await _innerService.SearchAsync(query, options);
        }

        public async Task<List<SearchResult>> VectorSearchAsync(string query, int maxResults = 10, int contentType = 0)
        {
            await _settingsService.LoadSettingsAsync();

            // Return empty if vector search is disabled
            if (!_settingsService.EnableVectorSearch)
            {
                return new List<SearchResult>();
            }

            return await _innerService.VectorSearchAsync(query, maxResults, contentType);
        }

        public async Task<List<SearchResult>> HybridSearchAsync(string query, HybridSearchOptions options)
        {
            await _settingsService.LoadSettingsAsync();

            // Fall back to keyword search if vector search is disabled
            if (!_settingsService.EnableVectorSearch)
            {
                return await _innerService.KeywordSearchAsync(query, options.MaxResults, options.ContentType);
            }

            return await _innerService.HybridSearchAsync(query, options);
        }

        // Delegate all other methods unchanged
        public Task<UnifiedDocument?> GetAsync(string documentId) =>
            _innerService.GetAsync(documentId);

        public Task<bool> UpdateAsync(string documentId, UnifiedDocument document) =>
            _innerService.UpdateAsync(documentId, document);

        public Task<bool> DeleteAsync(string documentId) =>
            _innerService.DeleteAsync(documentId);

        public Task<int> DeleteBatchAsync(IEnumerable<string> documentIds) =>
            _innerService.DeleteBatchAsync(documentIds);

        public Task<List<UnifiedDocument>> GetAllDocumentsAsync(int offset = 0, int limit = 100, int contentType = 0, bool onlyParentDocs = false) =>
            _innerService.GetAllDocumentsAsync(offset, limit, contentType, onlyParentDocs);

        public Task<List<UnifiedDocument>> GetChildDocumentsAsync(string parentId) =>
            _innerService.GetChildDocumentsAsync(parentId);

        public Task<List<SearchResult>> KeywordSearchAsync(string query, int maxResults = 10, int contentType = 0) =>
            _innerService.KeywordSearchAsync(query, maxResults, contentType);

        public Task<List<UnifiedDocument>> SearchByTagsAsync(int contentType = 0, params string[] tags) =>
            _innerService.SearchByTagsAsync(contentType, tags);

        public Task<List<UnifiedDocument>> SearchByMetadataAsync(string key, string value, int maxResults = 25) =>
            _innerService.SearchByMetadataAsync(key, value, maxResults);

        public Task<List<UnifiedDocument>> SearchBySourceUrlAsync(string sourceUrl, bool exactMatch = false, int maxResults = 25) =>
            _innerService.SearchBySourceUrlAsync(sourceUrl, exactMatch, maxResults);

        public Task<int> CountDocumentsAsync() =>
            _innerService.CountDocumentsAsync();

        public Task<string> CreateUserAsync(string email, string? username = null) =>
            _innerService.CreateUserAsync(email, username);

        public Task<string> CreateGroupAsync(string groupName, string? description = null) =>
            _innerService.CreateGroupAsync(groupName, description);

        public Task<bool> AddUserToGroupAsync(string userId, string groupId) =>
            _innerService.AddUserToGroupAsync(userId, groupId);

        public Task<bool> RemoveUserFromGroupAsync(string userId, string groupId) =>
            _innerService.RemoveUserFromGroupAsync(userId, groupId);

        public Task<bool> GrantDocumentAccessAsync(string documentId, string groupId, AccessLevel level = AccessLevel.Read) =>
            _innerService.GrantDocumentAccessAsync(documentId, groupId, level);

        public Task<bool> RevokeDocumentAccessAsync(string documentId, string groupId) =>
            _innerService.RevokeDocumentAccessAsync(documentId, groupId);

        public Task<bool> UserHasAccessAsync(string userId, string documentId) =>
            _innerService.UserHasAccessAsync(userId, documentId);

        public Task RebuildIndicesAsync() =>
            _innerService.RebuildIndicesAsync();

        public Task OptimizeAsync() =>
            _innerService.OptimizeAsync();

        public Task<DatabaseStatistics> GetStatisticsAsync() =>
            _innerService.GetStatisticsAsync();

        public Task<EmbeddingHealthStatus> GetEmbeddingHealthAsync() =>
            _innerService.GetEmbeddingHealthAsync();

        public Task ClearAllDataAsync() =>
            _innerService.ClearAllDataAsync();

        public Task<SyncResult> SyncAsync() =>
            _innerService.SyncAsync();

        public Task<int> GetPendingChangesCountAsync() =>
            _innerService.GetPendingChangesCountAsync();

		public Task<bool> DeleteByTypeAsync(int contentType) =>
			_innerService.DeleteByTypeAsync(contentType);

		public Task<bool> ResolveConflictsAsync(UnifiedData.Maui.ConflictResolution resolution) =>
            _innerService.ResolveConflictsAsync(resolution);

        public Task StoreDocumentTopicsAsync(string documentId, List<(string topic, float confidence)> topics, string classifierVersion = "1.0") =>
            _innerService.StoreDocumentTopicsAsync(documentId, topics, classifierVersion);

        public Task<List<UnifiedTopic>> GetDocumentTopicsAsync(string documentId) =>
            _innerService.GetDocumentTopicsAsync(documentId);

        public Task<List<UnifiedTopic>> GetAllTopicsAsync() =>
            _innerService.GetAllTopicsAsync();

        public Task<List<UnifiedDocument>> GetDocumentsByTopicAsync(string topicId, float minConfidence = 0f, int contentType = 0, int limit = 100) =>
            _innerService.GetDocumentsByTopicAsync(topicId, minConfidence, contentType, limit);

        public Task<List<UnifiedDocument>> GetDocumentsByRootId(string rootId) =>
            _innerService.GetDocumentsByRootId(rootId);
    }
}