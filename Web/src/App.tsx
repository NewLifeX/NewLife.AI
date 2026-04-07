import { useEffect, useCallback, useState } from 'react'
import { useNavigate, useParams, Routes, Route, Navigate } from 'react-router-dom'
import { ChatLayout } from '@/layouts/ChatLayout'
import { WelcomePage } from '@/pages/WelcomePage'
import { ChatPage } from '@/pages/ChatPage'
import { SharePage } from '@/pages/SharePage'
import { ModelSelector } from '@/components/chat/ModelSelector'
import { SettingsModal } from '@/components/settings/SettingsModal'
import { useChatStore, useSettingsStore, useUIStore } from '@/stores'
import { fetchUserProfile, fetchSystemConfig, type SuggestedQuestion } from '@/lib/api'
import { AppSkeleton } from '@/components/common/AppSkeleton'
import { ToastContainer } from '@/components/common/Toast'

function ChatApp() {
  const { conversationId } = useParams<{ conversationId: string }>()
  const navigate = useNavigate()

  const {
    conversations,
    activeConversationId,
    messages,
    isGenerating,
    isLoadingMessages,
    thinkingMode,
    loadConversations,
    setActiveConversation,
    newChat,
    sendMessage,
    stopGenerating,
    setThinkingMode,
    copyMessage,
    regenerateMsg,
    likeMsg,
    dislikeMsg,
    pendingAttachments,
    addAttachment,
    removeAttachment,
    models,
    loadModels,
    switchModel,
    deleteConversation: deleteConv,
    pinConversation: pinConv,
    renameConversation: renameConv,
  } = useChatStore()

  const settings = useSettingsStore()
  const loadSettingsFromServer = settings.loadFromServer
  const reloadSettingsFromServer = settings.reloadFromServer
  const { settingsOpen, openSettings, closeSettings, sidebarCollapsed, toggleSidebar } = useUIStore()
  const [userName, setUserName] = useState<string | undefined>(undefined)
  const [userAvatar, setUserAvatar] = useState<string | undefined>(undefined)
  const [appReady, setAppReady] = useState(false)
  const [siteTitle, setSiteTitle] = useState('智能助手')
  const [suggestedQuestions, setSuggestedQuestions] = useState<SuggestedQuestion[]>([])

  const handleNewChat = useCallback(() => {
    newChat()
    navigate('/chat')
  }, [newChat, navigate])

  useEffect(() => {
    Promise.all([
      loadConversations(),
      loadModels(),
      loadSettingsFromServer(),
      fetchUserProfile()
        .then((p) => { setUserName(p.nickname || p.account); setUserAvatar(p.avatar ?? undefined) })
        .catch(() => {}),
      fetchSystemConfig()
        .then((cfg) => {
          setSiteTitle(cfg.siteTitle)
          document.title = cfg.siteTitle
          setSuggestedQuestions(cfg.suggestedQuestions)
        })
        .catch(() => {}),
    ]).finally(() => setAppReady(true))

    // Cmd+K / Ctrl+K 全局快捷键 → 新建对话
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault()
        handleNewChat()
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [handleNewChat, loadConversations, loadModels, loadSettingsFromServer])

  // 打开设置页时重新拉取最新设置
  useEffect(() => {
    if (settingsOpen) reloadSettingsFromServer()
  }, [settingsOpen, reloadSettingsFromServer])

  useEffect(() => {
    const urlId = conversationId || undefined
    // 直接读取最新 store 值，避免将 activeConversationId 加入依赖导致 sendMessage
    // 设置 activeConversationId 后 URL 尚未更新时，effect 误判并清空会话的竞态问题
    const storeId = useChatStore.getState().activeConversationId
    if (urlId !== storeId) {
      if (urlId != null) {
        setActiveConversation(urlId)
      } else if (!conversationId) {
        setActiveConversation(undefined)
      }
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [conversationId, setActiveConversation])

  useEffect(() => {
    const expectedPath = activeConversationId != null
      ? `/chat/${activeConversationId}`
      : '/chat'
    if (window.location.pathname !== expectedPath) {
      navigate(expectedPath, { replace: true })
    }
  }, [activeConversationId, navigate])

  const handleConversationSelect = useCallback((id: string) => {
    navigate(`/chat/${id}`)
  }, [navigate])

  const handleDeleteConv = useCallback(async (id: string) => {
    await deleteConv(id)
    if (activeConversationId === id) {
      navigate('/chat')
    }
  }, [deleteConv, activeConversationId, navigate])

  const isWelcome = messages.length === 0
  const activeConv = conversations.find((c) => c.id === activeConversationId)
  const resolvedModel = activeConv?.modelId ?? settings.defaultModel ?? 0
  const currentModel = resolvedModel || models[0]?.id || 0
  const supportsThinking = models.find((m) => m.id === currentModel)?.supportThinking ?? false

  // 当前模型不支持思考时，若已选了 think 模式则自动回退到 auto
  useEffect(() => {
    if (!supportsThinking && (thinkingMode === 'think' || thinkingMode === 'fast')) {
      setThinkingMode('auto')
    }
  }, [supportsThinking, thinkingMode, setThinkingMode])

  if (!appReady) return <AppSkeleton />

  return (
    <>
      <ChatLayout
        conversations={conversations}
        activeConversationId={activeConversationId}
        onConversationSelect={handleConversationSelect}
        onConversationDelete={handleDeleteConv}
        onConversationPin={pinConv}
        onConversationRename={renameConv}
        onNewChat={handleNewChat}
        onSettingsOpen={openSettings}
        sidebarCollapsed={sidebarCollapsed}
        onSidebarToggle={toggleSidebar}
        onLoadMore={() => useChatStore.getState().loadMoreConversations()}
        onFileDrop={addAttachment}
        conversationTitle={activeConv?.title}
        userName={userName}
        userAvatar={userAvatar}
        modelSelector={
          <ModelSelector
            models={models}
            currentModel={currentModel}
            defaultModelId={settings.defaultModel || undefined}
            onModelChange={(modelId) => {
              if (activeConversationId != null) {
                switchModel(modelId)
              } else {
                settings.update({ defaultModel: modelId })
              }
            }}
          />
        }
      >
        {isWelcome ? (
          <WelcomePage
            onSend={sendMessage}
            siteTitle={siteTitle}
            suggestedQuestions={suggestedQuestions}
            attachments={pendingAttachments}
            onAttachmentAdd={addAttachment}
            onAttachmentRemove={removeAttachment}
          />
        ) : (
          <ChatPage
            messages={messages}
            isGenerating={isGenerating}
            isLoadingMessages={isLoadingMessages}
            onSend={sendMessage}
            onStop={stopGenerating}
            onCopy={copyMessage}
            onRegenerate={regenerateMsg}
            onEditSubmit={(id, content) => useChatStore.getState().editMsg(id, content)}
            onEditSaveOnly={(id, content) => useChatStore.getState().editMsgOnly(id, content)}
            onDelete={(id) => useChatStore.getState().deleteMsg(id)}
            onLike={likeMsg}
            onDislike={(id, reasons) => dislikeMsg(id, reasons)}
            conversationId={activeConversationId}
            thinkingMode={thinkingMode}
            onThinkingModeChange={setThinkingMode}
            supportsThinking={supportsThinking}
            attachments={pendingAttachments}
            onAttachmentAdd={addAttachment}
            onAttachmentRemove={removeAttachment}
            sendShortcut={settings.sendShortcut}
          />
        )}
      </ChatLayout>

      <SettingsModal
        open={settingsOpen}
        onClose={closeSettings}
        settings={settings}
        onSettingsChange={settings.update}
        models={models}
        onDataCleared={() => {
          loadConversations()
          handleNewChat()
        }}
      />
    </>
  )
}

function App() {
  return (
    <>
      <Routes>
        <Route path="/share/:token" element={<SharePage />} />
        <Route path="/chat/:conversationId" element={<ChatApp />} />
        <Route path="/chat" element={<ChatApp />} />
        <Route path="*" element={<Navigate to="/chat" replace />} />
      </Routes>
      <ToastContainer />
    </>
  )
}

export default App
