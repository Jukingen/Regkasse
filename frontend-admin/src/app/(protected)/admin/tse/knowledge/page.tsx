'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Card,
  Collapse,
  Drawer,
  Input,
  List,
  Rate,
  Space,
  Tabs,
  Tag,
  Typography,
} from 'antd';
import { useMemo, useState } from 'react';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import {
  getPopularTseKnowledgeArticles,
  getTseKnowledgeArticle,
  getTseKnowledgeFaqs,
  searchTseKnowledgeArticles,
  submitTseKnowledgeFeedback,
} from '@/features/tse-knowledge/api/knowledge';
import type { TseKnowledgeArticle } from '@/features/tse-knowledge/types';
import { useNotify } from '@/hooks/useNotify';
import { useI18n } from '@/i18n/I18nProvider';
import { adminOverviewCrumb } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';

const KEY = ['admin', 'tse-knowledge'] as const;

export default function TseKnowledgePage() {
  const { t } = useI18n();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const { hasPermission } = usePermissions();
  const allowed = hasPermission(PERMISSIONS.SYSTEM_CRITICAL);
  const [search, setSearch] = useState('');
  const [activeQuery, setActiveQuery] = useState<string | undefined>();
  const [selectedId, setSelectedId] = useState<string | undefined>();

  const popularQuery = useQuery({
    queryKey: [...KEY, 'popular'],
    queryFn: ({ signal }) => getPopularTseKnowledgeArticles(10, signal),
    enabled: allowed,
  });

  const faqQuery = useQuery({
    queryKey: [...KEY, 'faq'],
    queryFn: ({ signal }) => getTseKnowledgeFaqs(signal),
    enabled: allowed,
  });

  const searchQuery = useQuery({
    queryKey: [...KEY, 'search', activeQuery],
    queryFn: ({ signal }) => searchTseKnowledgeArticles(activeQuery, signal),
    enabled: allowed && !!activeQuery,
  });

  const articleQuery = useQuery({
    queryKey: [...KEY, 'article', selectedId],
    queryFn: ({ signal }) => getTseKnowledgeArticle(selectedId!, signal),
    enabled: allowed && !!selectedId,
  });

  const feedbackMutation = useMutation({
    mutationFn: (rating: number) => submitTseKnowledgeFeedback(selectedId!, rating),
    onSuccess: async () => {
      notify.success(t('tseKnowledge.feedbackThanks'));
      await queryClient.invalidateQueries({ queryKey: [...KEY] });
    },
    onError: (err) => {
      notify.apiError(err, {
        logContext: 'TseKnowledge.feedback',
        fallbackKey: 'common.errorGeneric',
      });
    },
  });

  const listSource: TseKnowledgeArticle[] = useMemo(() => {
    if (activeQuery) return searchQuery.data ?? [];
    return popularQuery.data ?? [];
  }, [activeQuery, searchQuery.data, popularQuery.data]);

  const faqItems = useMemo(
    () =>
      (faqQuery.data ?? []).map((faq) => ({
        key: faq.id,
        label: faq.title,
        children: (
          <Typography.Paragraph style={{ marginBottom: 0 }}>{faq.body}</Typography.Paragraph>
        ),
      })),
    [faqQuery.data]
  );

  if (!allowed) {
    return <Alert type="error" showIcon message={t('tseKnowledge.forbidden')} />;
  }

  const renderArticleItem = (article: TseKnowledgeArticle) => (
    <List.Item
      key={article.id}
      style={{ cursor: 'pointer' }}
      onClick={() => setSelectedId(article.id)}
    >
      <List.Item.Meta
        title={article.title}
        description={
          <Space direction="vertical" size={4} style={{ width: '100%' }}>
            <Typography.Text type="secondary">{article.description}</Typography.Text>
            <Space size="small" wrap>
              <Tag>{article.category}</Tag>
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {t('tseKnowledge.ratingLabel', {
                  rating: article.rating > 0 ? article.rating.toFixed(1) : '—',
                  views: article.viewCount,
                })}
              </Typography.Text>
            </Space>
          </Space>
        }
      />
    </List.Item>
  );

  return (
    <>
      <AdminPageHeader
        title={t('tseKnowledge.title')}
        breadcrumbs={[adminOverviewCrumb(t), { title: t('tseKnowledge.title') }]}
      />

      <Typography.Paragraph type="secondary">{t('tseKnowledge.subtitle')}</Typography.Paragraph>

      <Card title={t('tseKnowledge.cardTitle')}>
        <Alert type="info" showIcon message={t('tseKnowledge.diagnosticNote')} style={{ marginBottom: 16 }} />

        <Input.Search
          allowClear
          placeholder={t('tseKnowledge.searchPlaceholder')}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          onSearch={(value) => {
            const q = value.trim();
            setActiveQuery(q || undefined);
            if (!q) {
              void queryClient.invalidateQueries({ queryKey: [...KEY, 'popular'] });
            }
          }}
          style={{ marginBottom: 16 }}
        />

        <Tabs
          items={[
            {
              key: 'popular',
              label: activeQuery ? t('tseKnowledge.tabResults') : t('tseKnowledge.tabPopular'),
              children: (
                <List
                  loading={activeQuery ? searchQuery.isLoading : popularQuery.isLoading}
                  locale={{ emptyText: t('tseKnowledge.empty') }}
                  dataSource={listSource}
                  renderItem={renderArticleItem}
                />
              ),
            },
            {
              key: 'faq',
              label: t('tseKnowledge.tabFaq'),
              children: (
                <Collapse
                  accordion
                  bordered={false}
                  items={faqItems}
                  style={{ background: 'transparent' }}
                />
              ),
            },
          ]}
        />
      </Card>

      <Drawer
        title={articleQuery.data?.title ?? t('tseKnowledge.articleTitle')}
        open={!!selectedId}
        onClose={() => setSelectedId(undefined)}
        width={640}
        destroyOnHidden
      >
        {articleQuery.isError ? (
          <Alert type="error" showIcon message={t('tseKnowledge.loadError')} />
        ) : (
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            <Space wrap>
              <Tag>{articleQuery.data?.category}</Tag>
              <Typography.Text type="secondary">
                {t('tseKnowledge.ratingLabel', {
                  rating:
                    articleQuery.data && articleQuery.data.rating > 0
                      ? articleQuery.data.rating.toFixed(1)
                      : '—',
                  views: articleQuery.data?.viewCount ?? 0,
                })}
              </Typography.Text>
            </Space>
            <Typography.Paragraph>{articleQuery.data?.description}</Typography.Paragraph>
            <Typography.Paragraph style={{ whiteSpace: 'pre-wrap' }}>
              {articleQuery.data?.body}
            </Typography.Paragraph>
            <div>
              <Typography.Text strong>{t('tseKnowledge.rateArticle')}</Typography.Text>
              <div>
                <Rate
                  disabled={feedbackMutation.isPending || !articleQuery.data}
                  onChange={(value) => feedbackMutation.mutate(value)}
                />
              </div>
            </div>
          </Space>
        )}
      </Drawer>
    </>
  );
}
