﻿using System;
using System.Linq;
using Examine;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Persistence.Querying;

namespace Umbraco.Examine
{

    /// <summary>
    /// Performs the data lookups required to rebuild a content index
    /// </summary>
    public class ContentIndexPopulator : IIndexPopulator
    {
        private readonly IContentService _contentService;
        private readonly IValueSetBuilder<IContent> _contentValueSetBuilder;

        /// <summary>
        /// This is a static query, it's parameters don't change so store statically
        /// </summary>
        private static IQuery<IContent> _publishedQuery;

        private readonly bool _supportUnpublishedContent;
        private readonly int? _parentId;

        /// <summary>
        /// Default constructor to lookup all content data
        /// </summary>
        /// <param name="contentService"></param>
        /// <param name="sqlContext"></param>
        /// <param name="contentValueSetBuilder"></param>
        public ContentIndexPopulator(IContentService contentService, ISqlContext sqlContext, IValueSetBuilder<IContent> contentValueSetBuilder)
            : this(true, null, contentService, sqlContext, contentValueSetBuilder)
        {   
        }

        /// <summary>
        /// Optional constructor allowing specifying custom query parameters
        /// </summary>
        /// <param name="supportUnpublishedContent"></param>
        /// <param name="parentId"></param>
        /// <param name="contentService"></param>
        /// <param name="sqlContext"></param>
        /// <param name="contentValueSetBuilder"></param>
        public ContentIndexPopulator(bool supportUnpublishedContent, int? parentId, IContentService contentService, ISqlContext sqlContext, IValueSetBuilder<IContent> contentValueSetBuilder)
        {
            if (sqlContext == null) throw new ArgumentNullException(nameof(sqlContext));
            _contentService = contentService ?? throw new ArgumentNullException(nameof(contentService));
            _contentValueSetBuilder = contentValueSetBuilder ?? throw new ArgumentNullException(nameof(contentValueSetBuilder));
            if (_publishedQuery != null)
                _publishedQuery = sqlContext.Query<IContent>().Where(x => x.Published);
            _supportUnpublishedContent = supportUnpublishedContent;
            _parentId = parentId;
        }

        public void Populate(params IIndexer[] indexes)
        {
            const int pageSize = 10000;
            var pageIndex = 0;

            var contentParentId = -1;
            if (_parentId.HasValue && _parentId.Value > 0)
            {
                contentParentId = _parentId.Value;
            }
            IContent[] content;

            do
            {
                long total;

                if (_supportUnpublishedContent)
                {
                    content = _contentService.GetPagedDescendants(contentParentId, pageIndex, pageSize, out total).ToArray();
                }
                else
                {
                    //add the published filter
                    //note: We will filter for published variants in the validator
                    content = _contentService.GetPagedDescendants(contentParentId, pageIndex, pageSize, out total,
                        _publishedQuery, Ordering.By("Path", Direction.Ascending)).ToArray();
                }

                if (content.Length > 0)
                {
                    foreach (var index in indexes)
                        index.IndexItems(_contentValueSetBuilder.GetValueSets(content));
                }

                pageIndex++;
            } while (content.Length == pageSize);
        }
    }
}
