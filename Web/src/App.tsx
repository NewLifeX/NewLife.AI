import { useEffect, useCallback, useState } from 'react'
import { useNavigate, useParams, Routes, Route, Navigate } from 'react-router-dom'
import { ChatLayout } from '@/layouts/ChatLayout'
import { WelcomePage } from '@/pages/WelcomePage'
import { ChatPage } from '@/pages/ChatPage'
import { ModelSelector } from '@/components/chat/ModelSelector'
import { SettingsModal } from '@/components/settings/SettingsModal'
import { useChatStore, useSettingsStore, useUIStore } from '@/stores'
import { fetchUserProfile } from '@/lib/api'

function ChatApp() {
  const { conversationId } = useParams<{ conversationId: string }>()
  const navigate = useNavigate()

  const {
    conversations,
    activeConversationId,
    messages,
    isGenerating,
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
  const { settingsOpen, openSettings, closeSettings, sidebarCollapsed, toggleSidebar } = useUIStore()
  const [userName, setUserName] = useState<string | undefined>(undefined)
  const [userAvatar, setUserAvatar] = useState<string | undefined>(undefined)

  useEffect(() => {
    loadConversations()
    loadModels()
    settings.loadFromServer()
    fetchUserProfile()
      .then((p) => { setUserName(p.nickname || p.account); setUserAvatar(p.avatar ?? undefined) })
      .catch(() => {})
  }, [])

  useEffect(() => {
    const urlId = conversationId ? Number(conversationId) : undefined
    if (urlId !== activeConversationId) {
      if (urlId != null && !isNaN(urlId)) {
        setActiveConversation(urlId)
      } else if (!conversationId) {
        setActiveConversation(undefined)
      }
    }
  }, [conversationId])

  useEffect(() => {
    const expectedPath = activeConversationId != null
      ? `/chat/${activeConversationId}`
      : '/chat'
    if (window.location.pathname !== expectedPath) {
      navigate(expectedPath, { replace: true })
    }
  }, [activeConversationId, navigate])

  const handleConversationSelect = useCallback((id: number) => {
    navigate(`/chat/${id}`)
  }, [navigate])

  const handleNewChat = useCallback(() => {
    newChat()
    navigate('/chat')
  }, [newChat, navigate])

  const handleDeleteConv = useCallback(async (id: number) => {
    await deleteConv(id)
    if (activeConversationId === id) {
      navigate('/chat')
    }
  }, [deleteConv, activeConversationId, navigate])

  const isWelcome = messages.length === 0
  const activeConv = conversations.find((c) => c.id === activeConversationId)
  const currentModel = activeConv?.modelCode ?? settings.defaultModel ?? 'qwen-max'

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
        userName={userName}
        userAvatar={userAvatar}
        modelSelector={
          <ModelSelector
            models={models}
            currentModel={currentModel}
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
          <WelcomePage onSend={sendMessage} />
        ) : (
          <ChatPage
            messages={messages}
            isGenerating={isGenerating}
            onSend={sendMessage}
            onStop={stopGenerating}
            onCopy={copyMessage}
            onRegenerate={regenerateMsg}
            onEditSubmit={(id, content) => useChatStore.getState().editMsg(id, content)}
            onLike={likeMsg}
            onDislike={dislikeMsg}
            thinkingMode={thinkingMode}
            onThinkingModeChange={setThinkingMode}
            attachments={pendingAttachments}
            onAttachmentAdd={addAttachment}
            onAttachmentRemove={removeAttachment}
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
    <Routes>
      <Route path="/chat/:conversationId" element={<ChatApp />} />
      <Route path="/chat" element={<ChatApp />} />
      <Route path="*" element={<Navigate to="/chat" replace />} />
    </Routes>
  )
}

export default App
