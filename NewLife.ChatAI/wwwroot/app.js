const modelSelect = document.getElementById("modelSelect");
const modeSelect = document.getElementById("modeSelect");
const sendButton = document.getElementById("sendButton");
const promptInput = document.getElementById("promptInput");
const messages = document.getElementById("messages");
const conversationList = document.getElementById("conversationList");
const newConversation = document.getElementById("newConversation");

let currentConversationId = null;

async function loadModels() {
    const response = await fetch("/api/models");
    const models = await response.json();
    modelSelect.innerHTML = "";
    for (const model of models) {
        const option = document.createElement("option");
        option.value = model.code;
        option.textContent = model.name;
        modelSelect.appendChild(option);
    }
}

async function loadConversations() {
    const response = await fetch("/api/conversations?page=1&pageSize=20");
    const result = await response.json();
    conversationList.innerHTML = "";
    for (const item of result.items) {
        const button = document.createElement("button");
        button.className = "primary";
        button.textContent = item.title;
        button.onclick = () => openConversation(item.id);
        conversationList.appendChild(button);
    }
}

function appendMessage(role, content) {
    const div = document.createElement("div");
    div.className = `message ${role}`;
    div.textContent = content;
    messages.appendChild(div);
    messages.scrollTop = messages.scrollHeight;
    return div;
}

async function createConversation() {
    const response = await fetch("/api/conversations", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ title: "新建对话", modelCode: modelSelect.value })
    });

    const item = await response.json();
    currentConversationId = item.id;
    messages.innerHTML = "";
    await loadConversations();
}

async function openConversation(conversationId) {
    currentConversationId = conversationId;
    messages.innerHTML = "";

    const response = await fetch(`/api/conversations/${conversationId}/messages`);
    const list = await response.json();
    for (const item of list) appendMessage(item.role, item.content);
}

async function send() {
    const content = promptInput.value.trim();
    if (!content) return;
    if (!currentConversationId) await createConversation();

    appendMessage("user", content);
    promptInput.value = "";

    const assistant = appendMessage("assistant", "");

    const response = await fetch(`/api/conversations/${currentConversationId}/messages`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ content, thinkingMode: modeSelect.value, attachmentIds: [] })
    });

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";

    while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n\n");
        buffer = lines.pop() || "";

        for (const line of lines) {
            if (!line.startsWith("data:")) continue;
            const payload = line.substring(5).trim();
            const data = JSON.parse(payload);
            if (data.type === "delta") assistant.textContent += data.content;
        }
    }

    await loadConversations();
}

newConversation.addEventListener("click", createConversation);
sendButton.addEventListener("click", send);
promptInput.addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        send();
    }
});

loadModels().then(loadConversations);
