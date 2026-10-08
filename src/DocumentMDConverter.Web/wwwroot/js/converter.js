const mermaidRenderTokens = new WeakMap();

window.converterUtils = {
    copyToClipboard: async function (text) {
        if (!navigator.clipboard) {
            const textArea = document.createElement("textarea");
            textArea.value = text;
            document.body.appendChild(textArea);
            textArea.select();
            document.execCommand("copy");
            document.body.removeChild(textArea);
            return true;
        }
        await navigator.clipboard.writeText(text);
        return true;
    },

    downloadMarkdownFile: function (fileName, textContent) {
        const blob = new Blob([textContent], { type: "text/markdown;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = fileName;
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    },

    downloadFromPost: async function (endpointUrl, payload, defaultFilename) {
        const response = await fetch(endpointUrl, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(payload)
        });
        if (!response.ok) {
            throw new Error(`Download failed with status ${response.status}`);
        }
        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = defaultFilename || "conversions.zip";
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        URL.revokeObjectURL(url);
    },

    getCurrentTheme: function () {
        return document.documentElement.getAttribute("data-theme") || "dark";
    },

    renderMermaid: async function (root) {
        if (!root || root.getClientRects().length === 0) return;

        const renderToken = {};
        mermaidRenderTokens.set(root, renderToken);
        root.querySelectorAll(".mermaid-render-error").forEach(error => error.remove());
        root.querySelectorAll('pre.mermaid[data-mermaid-rendered="error"]').forEach(diagram => {
            delete diagram.dataset.mermaidRendered;
            delete diagram.dataset.processed;
        });

        const diagrams = Array.from(root.querySelectorAll(
            "pre.mermaid:not([data-mermaid-rendered]), div.mermaid:not([data-mermaid-rendered])"
        ));
        if (diagrams.length === 0) return;

        diagrams.forEach((diagram, index) => {
            if (diagram.tagName !== "DIV") return;

            const source = Array.from(diagram.children).map(child => child.textContent).join("\n") || diagram.textContent;
            const codeBlock = document.createElement("pre");
            codeBlock.className = "mermaid";
            codeBlock.textContent = source;
            diagram.replaceWith(codeBlock);
            diagrams[index] = codeBlock;
        });

        let mermaid;
        try {
            ({ default: mermaid } = await import("https://cdn.jsdelivr.net/npm/mermaid@latest/dist/mermaid.esm.min.mjs"));
        } catch (error) {
            if (mermaidRenderTokens.get(root) !== renderToken) return;
            diagrams.forEach(diagram => {
                if (!diagram.isConnected) return;
                diagram.dataset.mermaidRendered = "error";
                const message = document.createElement("div");
                message.className = "mermaid-render-error";
                message.setAttribute("role", "status");
                message.textContent = "Could not load Mermaid.js; showing source.";
                diagram.after(message);
            });
            console.error("Mermaid.js failed to load.", error);
            return;
        }

        const isLightTheme = this.getCurrentTheme() === "light";
        mermaid.initialize({
            startOnLoad: false,
            securityLevel: "strict",
            theme: "base",
            themeVariables: {
                background: isLightTheme ? "#ffffff" : "#13161c",
                primaryColor: "#e8edf7",
                primaryTextColor: "#172033",
                primaryBorderColor: "#68779c",
                secondaryColor: "#e5f1e9",
                secondaryTextColor: "#172033",
                secondaryBorderColor: "#668374",
                tertiaryColor: "#f2ebf8",
                tertiaryTextColor: "#172033",
                tertiaryBorderColor: "#89739d",
                nodeTextColor: "#172033",
                lineColor: isLightTheme ? "#64748b" : "#aab8c8",
                textColor: "#172033",
                titleColor: isLightTheme ? "#0f172a" : "#172033",
                clusterBkg: "#e8edf3",
                clusterBorder: "#94a3b8",
                edgeLabelBackground: isLightTheme ? "#ffffff" : "#e2e8f0",
                noteBkgColor: "#fff5ad",
                noteTextColor: "#172033"
            }
        });

        for (const diagram of diagrams) {
            diagram.dataset.mermaidRendered = "true";
            try {
                await mermaid.run({ nodes: [diagram] });
            } catch (error) {
                if (diagram.isConnected && mermaidRenderTokens.get(root) === renderToken) {
                    diagram.dataset.mermaidRendered = "error";
                    const message = document.createElement("div");
                    message.className = "mermaid-render-error";
                    message.setAttribute("role", "status");
                    message.textContent = "Could not render this Mermaid diagram. Check its syntax.";
                    diagram.after(message);
                }
                console.error("Mermaid diagram rendering failed.", error);
            }
        }
    },

    // Blazor must not own nodes Mermaid mutates, or its next diff throws.
    setHtml: function (el, html) {
        if (el) el.innerHTML = html;
    },

    toggleTheme: function () {
        const currentTheme = document.documentElement.getAttribute("data-theme") || "dark";
        const nextTheme = currentTheme === "dark" ? "light" : "dark";
        document.documentElement.setAttribute("data-theme", nextTheme);
        localStorage.setItem("mdai-theme", nextTheme);
        return nextTheme;
    },

    initGlobalDropzone: function (inputId, overlayId) {
        if (window.__converterDropzoneInitialized) return;
        window.__converterDropzoneInitialized = true;

        function feedFilesToInput(files) {
            const currentInput = document.getElementById(inputId);
            if (!currentInput || !files || files.length === 0) return;
            try {
                const dt = new DataTransfer();
                for (let i = 0; i < files.length; i++) {
                    dt.items.add(files[i]);
                }
                currentInput.files = dt.files;
                currentInput.dispatchEvent(new Event("change", { bubbles: true }));
            } catch (err) {
                console.error("Failed to forward files via DataTransfer:", err);
            }
        }

        let dragCounter = 0;

        window.addEventListener("dragenter", function (e) {
            if (e.dataTransfer && e.dataTransfer.types && Array.from(e.dataTransfer.types).includes("Files")) {
                dragCounter++;
                const overlay = document.getElementById(overlayId);
                if (overlay) {
                    overlay.classList.add("active");
                }
            }
        });

        window.addEventListener("dragleave", function (e) {
            dragCounter--;
            if (dragCounter <= 0) {
                dragCounter = 0;
                const overlay = document.getElementById(overlayId);
                if (overlay) {
                    overlay.classList.remove("active");
                }
            }
        });

        window.addEventListener("dragover", function (e) {
            if (e.dataTransfer && e.dataTransfer.types && Array.from(e.dataTransfer.types).includes("Files")) {
                e.preventDefault();
            }
        });

        window.addEventListener("drop", function (e) {
            dragCounter = 0;
            const overlay = document.getElementById(overlayId);
            if (overlay) {
                overlay.classList.remove("active");
            }
            if (e.dataTransfer && e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                e.preventDefault();
                feedFilesToInput(e.dataTransfer.files);
            }
        });

        window.addEventListener("paste", function (e) {
            const activeEl = document.activeElement;
            const isTyping = activeEl && (activeEl.tagName === "INPUT" || activeEl.tagName === "TEXTAREA") && activeEl.id !== inputId;

            if (e.clipboardData) {
                const files = e.clipboardData.files;
                if (files && files.length > 0) {
                    e.preventDefault();
                    feedFilesToInput(files);
                    return;
                }

                const items = e.clipboardData.items;
                if (items) {
                    const imageFiles = [];
                    for (let i = 0; i < items.length; i++) {
                        if (items[i].type.indexOf("image") !== -1) {
                            const blob = items[i].getAsFile();
                            if (blob) {
                                const now = new Date();
                                const timestamp = `${now.getFullYear()}${String(now.getMonth() + 1).padStart(2, '0')}${String(now.getDate()).padStart(2, '0')}-${String(now.getHours()).padStart(2, '0')}${String(now.getMinutes()).padStart(2, '0')}${String(now.getSeconds()).padStart(2, '0')}`;
                                const ext = blob.type === "image/jpeg" ? ".jpg" : (blob.type === "image/webp" ? ".webp" : ".png");
                                const file = new File([blob], `screenshot-${timestamp}${ext}`, { type: blob.type });
                                imageFiles.push(file);
                            }
                        }
                    }
                    if (imageFiles.length > 0) {
                        e.preventDefault();
                        feedFilesToInput(imageFiles);
                        return;
                    }
                }
            }
        });
    },

    pasteFromClipboard: async function (inputId) {
        if (!navigator.clipboard || !navigator.clipboard.read) {
            return false;
        }
        try {
            const clipboardItems = await navigator.clipboard.read();
            const files = [];
            for (const item of clipboardItems) {
                for (const type of item.types) {
                    if (type.startsWith("image/")) {
                        const blob = await item.getType(type);
                        const ext = type === "image/jpeg" ? ".jpg" : (type === "image/webp" ? ".webp" : ".png");
                        const file = new File([blob], `pasted-${Date.now()}${ext}`, { type: type });
                        files.push(file);
                        break;
                    }
                }
            }
            if (files.length > 0) {
                const currentInput = document.getElementById(inputId);
                if (currentInput) {
                    const dt = new DataTransfer();
                    for (const f of files) dt.items.add(f);
                    currentInput.files = dt.files;
                    currentInput.dispatchEvent(new Event("change", { bubbles: true }));
                    return true;
                }
            }
            return false;
        } catch (err) {
            console.warn("Could not read clipboard directly:", err);
            return false;
        }
    }
};

function setSplitRatio(container, handle, ratio) {
    const value = Math.min(75, Math.max(25, ratio));
    container.style.setProperty("--split-left", `${value}fr`);
    container.style.setProperty("--split-right", `${100 - value}fr`);
    handle.setAttribute("aria-valuenow", String(Math.round(value)));
}

document.addEventListener("pointerdown", function (event) {
    const target = event.target instanceof Element ? event.target : null;
    const handle = target && target.closest(".split-pane-resizer");
    const container = handle && handle.parentElement;
    if (!handle || !container || !container.classList.contains("resizable-split") || event.button !== 0) return;

    event.preventDefault();
    handle.setPointerCapture(event.pointerId);

    function move(moveEvent) {
        if (moveEvent.pointerId !== event.pointerId) return;
        const bounds = container.getBoundingClientRect();
        const availableWidth = bounds.width - handle.offsetWidth;
        if (availableWidth <= 0) return;
        setSplitRatio(container, handle, ((moveEvent.clientX - bounds.left - handle.offsetWidth / 2) / availableWidth) * 100);
    }

    function stop(stopEvent) {
        if (stopEvent.pointerId !== event.pointerId) return;
        handle.removeEventListener("pointermove", move);
        handle.removeEventListener("pointerup", stop);
        handle.removeEventListener("pointercancel", stop);
        if (handle.hasPointerCapture(event.pointerId)) handle.releasePointerCapture(event.pointerId);
    }

    handle.addEventListener("pointermove", move);
    handle.addEventListener("pointerup", stop);
    handle.addEventListener("pointercancel", stop);
});

document.addEventListener("keydown", function (event) {
    const target = event.target instanceof Element ? event.target : null;
    const handle = target && target.closest(".split-pane-resizer");
    const container = handle && handle.parentElement;
    if (!handle || !container || !container.classList.contains("resizable-split")) return;

    const current = Number(handle.getAttribute("aria-valuenow")) || 50;
    const step = event.shiftKey ? 10 : 2;
    if (event.key === "ArrowLeft") {
        event.preventDefault();
        setSplitRatio(container, handle, current - step);
    } else if (event.key === "ArrowRight") {
        event.preventDefault();
        setSplitRatio(container, handle, current + step);
    } else if (event.key === "Home") {
        event.preventDefault();
        setSplitRatio(container, handle, 25);
    } else if (event.key === "End") {
        event.preventDefault();
        setSplitRatio(container, handle, 75);
    }
});
