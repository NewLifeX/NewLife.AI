import { ChatLayout } from '@/layouts/ChatLayout'
import { WelcomePage } from '@/pages/WelcomePage'
import { ChatPage } from '@/pages/ChatPage'
import { SettingsModal } from '@/components/settings/SettingsModal'
import { useChatStore, useSettingsStore, useUIStore } from '@/stores'

function App() {
  const {
    conversations,
    activeConversationId,
    messages,
    isGenerating,
    setActiveConversation,
    newChat,
    sendMessage,
    stopGenerating,
    copyMessage,
  } = useChatStore()

  const settings = useSettingsStore()
  const { settingsOpen, openSettings, closeSettings } = useUIStore()

  const isWelcome = messages.length === 0

  return (
    <>
      <ChatLayout
        conversations={conversations}
        activeConversationId={activeConversationId}
        onConversationSelect={setActiveConversation}
        onNewChat={newChat}
        onSettingsOpen={openSettings}
        userName="用户"
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
          />
        )}
      </ChatLayout>

      <SettingsModal
        open={settingsOpen}
        onClose={closeSettings}
        settings={settings}
        onSettingsChange={settings.update}
      />
    </>
  )
}

export default App
