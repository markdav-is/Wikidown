window.wikidown = {
  downloadFile(fileName, contentType, bytes) {
    const blob = new Blob([bytes], { type: contentType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
  },

  // Monaco is a code editor; these make it behave for prose: lists continue
  // on Enter, images can be pasted or dropped in, and the preview follows
  // the editor's scroll position.
  enhanceEditor(id, dotnetRef, previewSelector, cursorAtEnd) {
    const editor = window.blazorMonaco?.editor?.getEditor(id, true);
    if (!editor) return;

    editor.updateOptions({
      quickSuggestions: false,
      wordBasedSuggestions: "off",
      occurrencesHighlight: "off",
      padding: { top: 12, bottom: 12 },
      lineDecorationsWidth: 12,
      unicodeHighlight: { ambiguousCharacters: false, invisibleCharacters: false },
      dropIntoEditor: { enabled: false },
      pasteAs: { enabled: false },
    });

    if (cursorAtEnd) {
      const last = editor.getModel().getLineCount();
      editor.setPosition({ lineNumber: last, column: editor.getModel().getLineMaxColumn(last) });
    }

    editor.onKeyDown(e => {
      if (e.keyCode !== monaco.KeyCode.Enter || e.shiftKey || e.ctrlKey || e.altKey || e.metaKey) return;
      const sel = editor.getSelection();
      if (!sel || !sel.isEmpty()) return;
      const lineNumber = sel.startLineNumber;
      const line = editor.getModel().getLineContent(lineNumber);
      if (sel.startColumn <= line.length) return;
      const m = /^(\s*(?:>\s*)*)([-*+]|\d+[.)])(\s+)(\[[ xX]\]\s+)?(.*)$/.exec(line);
      if (!m) return;
      e.preventDefault();
      e.stopPropagation();

      if (m[5].trim() === "") {
        // Enter on an empty item ends the list, leaving the blank line a new paragraph needs.
        editor.executeEdits("wikidown", [{ range: new monaco.Range(lineNumber, 1, lineNumber, line.length + 1), text: "\n" }],
          [new monaco.Selection(lineNumber + 1, 1, lineNumber + 1, 1)]);
        return;
      }
      const numbered = /^(\d+)([.)])$/.exec(m[2]);
      const marker = numbered ? (parseInt(numbered[1], 10) + 1) + numbered[2] : m[2];
      const prefix = m[1] + marker + m[3] + (m[4] ? "[ ] " : "");
      const column = prefix.length + 1;
      editor.executeEdits("wikidown", [{ range: sel, text: "\n" + prefix, forceMoveMarkers: true }],
        [new monaco.Selection(lineNumber + 1, column, lineNumber + 1, column)]);
      editor.revealLine(lineNumber + 1);
    });

    const send = file => new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => reject(reader.error);
      reader.onload = () => {
        const dataUrl = String(reader.result);
        resolve(dotnetRef.invokeMethodAsync("OnImageFile", file.name || "image.png", file.type, dataUrl.slice(dataUrl.indexOf(",") + 1)));
      };
      reader.readAsDataURL(file);
    });
    const images = list => Array.from(list || []).filter(f => f.type.startsWith("image/"));
    // Monaco's own paste/drop handlers sit on its root node and stop the
    // event there, so these listen one level up, where capture reaches first.
    const node = editor.getContainerDomNode().parentElement || editor.getContainerDomNode();
    node.addEventListener("paste", async e => {
      const files = images(e.clipboardData && e.clipboardData.files);
      if (files.length === 0) return;
      e.preventDefault();
      e.stopPropagation();
      for (const file of files) await send(file);
    }, true);
    node.addEventListener("dragover", e => {
      if (e.dataTransfer && Array.from(e.dataTransfer.types || []).includes("Files")) e.preventDefault();
    }, true);
    node.addEventListener("drop", async e => {
      const files = images(e.dataTransfer && e.dataTransfer.files);
      if (files.length === 0) return;
      e.preventDefault();
      e.stopPropagation();
      const target = editor.getTargetAtClientPoint(e.clientX, e.clientY);
      if (target && target.position) editor.setPosition(target.position);
      for (const file of files) await send(file);
    }, true);

    editor.onDidScrollChange(() => {
      const preview = document.querySelector(previewSelector);
      if (!preview) return;
      const range = editor.getScrollHeight() - editor.getLayoutInfo().height;
      const ratio = range > 0 ? editor.getScrollTop() / range : 0;
      preview.scrollTop = ratio * (preview.scrollHeight - preview.clientHeight);
    });
  },

  toPngDataUrl(dataUrl) {
    return new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => {
        const canvas = document.createElement("canvas");
        canvas.width = img.naturalWidth || img.width;
        canvas.height = img.naturalHeight || img.height;
        canvas.getContext("2d").drawImage(img, 0, 0);
        resolve(canvas.toDataURL("image/png"));
      };
      img.onerror = () => reject(new Error("image decode failed"));
      img.src = dataUrl;
    });
  }
};
