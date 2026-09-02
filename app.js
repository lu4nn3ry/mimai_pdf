// PDF.js worker configuration
if (typeof pdfjsLib !== 'undefined') {
  pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
}

// State Management
let pdfDoc = null;
let currentPage = 1;
let totalPages = 0;
let pageRendering = false;
let pageNumPending = null;
let scale = 1.2;
let pageTranslations = {}; // pageNum -> translatedText
let pageParagraphs = {};   // pageNum -> Array of { id, text, left, top, width, height, translation }
let isTranslating = false;
let currentAbortController = null;
let overlayOpacity = 0.95;

// DOM Elements
const pdfUpload = document.getElementById('pdf-upload');
const pdfFilename = document.getElementById('pdf-filename');
const canvas = document.getElementById('pdf-render-canvas');
const ctx = canvas.getContext('2d');
const canvasWrapper = document.getElementById('canvas-wrapper');
const textOverlayLayer = document.getElementById('text-overlay-layer');
const noPdfPlaceholder = document.getElementById('no-pdf-placeholder');
const currentPageNum = document.getElementById('current-page-num');
const totalPageCount = document.getElementById('total-page-count');
const prevPageBtn = document.getElementById('prev-page-btn');
const nextPageBtn = document.getElementById('next-page-btn');
const zoomInBtn = document.getElementById('zoom-in-btn');
const zoomOutBtn = document.getElementById('zoom-out-btn');
const zoomLevel = document.getElementById('zoom-level');
const fitWidthBtn = document.getElementById('fit-width-btn');

const toggleOverlayCheckbox = document.getElementById('toggle-overlay-checkbox');
const overlayOpacitySlider = document.getElementById('overlay-opacity-slider');
const modeSplitBtn = document.getElementById('mode-split-btn');
const modeOverlayBtn = document.getElementById('mode-overlay-btn');
const translationSidebar = document.getElementById('translation-sidebar');

const modelSelect = document.getElementById('model-select');
const gpuSelect = document.getElementById('gpu-select');
const refreshModelsBtn = document.getElementById('refresh-models-btn');
const targetLangSelect = document.getElementById('target-lang');
const translatePageBtn = document.getElementById('translate-page-btn');
const translateAllBtn = document.getElementById('translate-all-btn');
const exportMdBtn = document.getElementById('export-md-btn');

const statusIndicator = document.getElementById('status-indicator');
const statusText = document.getElementById('status-text');
const noTranslationPlaceholder = document.getElementById('no-translation-placeholder');
const translationMarkdown = document.getElementById('translation-markdown');
const copyTranslationBtn = document.getElementById('copy-translation-btn');
const clearTranslationBtn = document.getElementById('clear-translation-btn');
const progressBarContainer = document.getElementById('progress-bar-container');
const progressBarFill = document.getElementById('progress-bar-fill');

// --- Persistent Translation Cache System ---
function getCacheKey(pageNum, blockId = 'full') {
  const filename = (pdfFilename.textContent || 'doc').trim();
  const lang = targetLangSelect.value;
  return `trans_cache::${filename}::p${pageNum}::${blockId}::${lang}`;
}

function saveToCache(pageNum, blockId, text) {
  try {
    if (!text || text.trim().length === 0) return;
    const key = getCacheKey(pageNum, blockId);
    localStorage.setItem(key, text);
  } catch (e) {
    console.warn('Cache save warning:', e);
  }
}

function getFromCache(pageNum, blockId = 'full') {
  try {
    const key = getCacheKey(pageNum, blockId);
    return localStorage.getItem(key);
  } catch (e) {
    return null;
  }
}

function clearPageCache(pageNum) {
  try {
    const prefix = `trans_cache::${(pdfFilename.textContent || 'doc').trim()}::p${pageNum}::`;
    Object.keys(localStorage).forEach((k) => {
      if (k.startsWith(prefix)) {
        localStorage.removeItem(k);
      }
    });
  } catch (e) {
    console.warn('Cache clear warning:', e);
  }
}

// Discover Ollama Models
async function loadOllamaModels() {
  try {
    setStatus('Conectando ao Ollama...', 'bg-amber-500');
    const res = await fetch('http://localhost:11434/api/tags', { method: 'GET' });
    if (!res.ok) throw new Error('Ollama offline');
    const data = await res.json();
    if (data.models && data.models.length > 0) {
      modelSelect.innerHTML = '';
      data.models.forEach((m) => {
        const opt = document.createElement('option');
        opt.value = m.name;
        opt.textContent = m.name;
        modelSelect.appendChild(opt);
      });
      const preferred = data.models.find(m => m.name.includes('qwen') || m.name.includes('llama') || m.name.includes('gemma'));
      if (preferred) modelSelect.value = preferred.name;
      setStatus('Pronto (Ollama Conectado)', 'bg-emerald-500');
    }
  } catch (err) {
    console.warn('Ollama tags error:', err);
    setStatus('Ollama offline (localhost:11434)', 'bg-rose-500');
  }
}

function setStatus(text, colorClass = 'bg-emerald-500') {
  statusText.textContent = text;
  statusIndicator.className = `w-2 h-2 rounded-full ${colorClass}`;
}

// Convert PDF.js coordinates accurately to Viewport Coordinates
async function extractPageParagraphs(pageNum, viewport) {
  if (!pdfDoc) return [];
  try {
    const page = await pdfDoc.getPage(pageNum);
    const textContent = await page.getTextContent();

    const rawItems = textContent.items.map((item) => {
      const tx = item.transform[4];
      const ty = item.transform[5];
      const fontHeight = Math.hypot(item.transform[2], item.transform[3]) || 12;

      const [x1, y1] = viewport.convertToViewportPoint(tx, ty + fontHeight);
      const [x2, y2] = viewport.convertToViewportPoint(tx + (item.width || 40), ty);

      const left = Math.min(x1, x2);
      const top = Math.min(y1, y2);
      const width = Math.max(10, Math.abs(x2 - x1));
      const height = Math.max(10, Math.abs(y2 - y1));

      return {
        str: item.str,
        left,
        top,
        width,
        height,
      };
    }).filter(item => item.str.trim().length > 0);

    if (rawItems.length === 0) return [];

    const clusters = [];
    let currentCluster = null;

    for (const item of rawItems) {
      if (!currentCluster) {
        currentCluster = {
          left: item.left,
          top: item.top,
          width: item.width,
          height: item.height,
          text: item.str,
        };
        continue;
      }

      const verticalDist = item.top - (currentCluster.top + currentCluster.height);
      const isSameOrNextLine = verticalDist >= -10 && verticalDist <= (18 * scale);

      if (isSameOrNextLine) {
        currentCluster.left = Math.min(currentCluster.left, item.left);
        currentCluster.top = Math.min(currentCluster.top, item.top);
        currentCluster.width = Math.max(currentCluster.width, (item.left + item.width) - currentCluster.left);
        currentCluster.height = (item.top + item.height) - currentCluster.top;
        currentCluster.text += (currentCluster.text.endsWith('-') ? '' : ' ') + item.str;
      } else {
        clusters.push(currentCluster);
        currentCluster = {
          left: item.left,
          top: item.top,
          width: item.width,
          height: item.height,
          text: item.str,
        };
      }
    }

    if (currentCluster) clusters.push(currentCluster);

    return clusters.map((c, i) => {
      const blockId = `p-${pageNum}-${i}`;
      const cached = getFromCache(pageNum, blockId);
      return {
        id: blockId,
        left: Math.max(0, c.left),
        top: Math.max(0, c.top),
        width: Math.max(80, c.width),
        height: Math.max(20, c.height),
        text: c.text.trim(),
        translation: cached || '',
      };
    });
  } catch (err) {
    console.error('Erro ao extrair parágrafos:', err);
    return [];
  }
}

// Render Overlay Elements on Top of Canvas
function renderOverlayLayer(paragraphs) {
  textOverlayLayer.innerHTML = '';
  if (!paragraphs || paragraphs.length === 0 || !toggleOverlayCheckbox.checked) return;

  paragraphs.forEach((p, idx) => {
    const el = document.createElement('div');
    el.id = p.id;
    el.className = 'overlay-block absolute pointer-events-auto rounded transition-all cursor-pointer font-sans';
    el.style.left = `${Math.max(4, p.left - 4)}px`;
    el.style.top = `${Math.max(4, p.top - 2)}px`;
    el.style.width = `${Math.max(120, p.width + 8)}px`;
    el.style.minHeight = `${Math.max(22, p.height + 4)}px`;

    const hasTranslation = p.translation && p.translation.trim().length > 0;

    if (hasTranslation) {
      el.style.backgroundColor = `rgba(15, 23, 42, ${overlayOpacity})`;
      el.style.zIndex = '25';
      el.className += ' text-slate-100 text-xs shadow-xl border border-indigo-500/50 backdrop-blur-md p-2.5';
      el.innerHTML = `
        <div class="flex items-center justify-between text-[10px] text-indigo-400 mb-1 select-none border-b border-indigo-500/20 pb-0.5">
          <span class="font-mono font-medium">#${idx + 1} Tradução <span class="text-[9px] bg-emerald-500/20 text-emerald-300 px-1 rounded">💾 Cache</span></span>
          <span class="hover:text-white" title="Re-traduzir"><i class="fa-solid fa-rotate-right"></i></span>
        </div>
        <div class="text-[12px] leading-relaxed text-slate-100 font-normal select-text prose prose-invert max-w-none">
          ${marked.parse(p.translation)}
        </div>
      `;
    } else {
      el.style.backgroundColor = 'transparent';
      el.style.zIndex = '10';
      el.className += ' text-transparent hover:text-white hover:bg-indigo-950/40 border border-transparent hover:border-indigo-500/30 p-1';
      el.title = 'Clique para traduzir este bloco específico';
      el.innerHTML = `<span class="text-[10px] bg-slate-900/90 text-indigo-300 px-1.5 py-0.5 rounded shadow opacity-0 hover:opacity-100"><i class="fa-solid fa-wand-magic-sparkles"></i> Traduzir</span>`;
    }

    el.addEventListener('click', () => handleTranslateSingleBlock(p));
    textOverlayLayer.appendChild(el);
  });
}

// Render Page
async function renderPage(num) {
  if (!pdfDoc) return;
  pageRendering = true;
  currentPageNum.textContent = num;

  try {
    const page = await pdfDoc.getPage(num);
    const viewport = page.getViewport({ scale });
    canvas.height = viewport.height;
    canvas.width = viewport.width;

    const renderContext = {
      canvasContext: ctx,
      viewport: viewport,
    };

    await page.render(renderContext).promise;
    pageRendering = false;

    // Load / Restore cached paragraphs & full translation
    if (!pageParagraphs[num]) {
      pageParagraphs[num] = await extractPageParagraphs(num, viewport);
    }
    renderOverlayLayer(pageParagraphs[num]);

    const cachedFull = pageTranslations[num] || getFromCache(num, 'full');
    if (cachedFull) {
      pageTranslations[num] = cachedFull;
      displayTranslation(cachedFull);
      setStatus('Tradução restaurada do Cache 💾', 'bg-emerald-500');
    } else {
      translationMarkdown.innerHTML = `<div class="text-slate-400 italic text-xs">Página ${num} carregada. Clique em "Traduzir Página" para iniciar.</div>`;
      translationMarkdown.classList.remove('hidden');
      noTranslationPlaceholder.classList.add('hidden');
    }

    if (pageNumPending !== null) {
      const pending = pageNumPending;
      pageNumPending = null;
      renderPage(pending);
    }

    prevPageBtn.disabled = num <= 1;
    nextPageBtn.disabled = num >= totalPages;
  } catch (err) {
    console.error('Error rendering page:', err);
    pageRendering = false;
  }
}

function queueRenderPage(num) {
  if (pageRendering) {
    pageNumPending = num;
  } else {
    renderPage(num);
  }
}

// Streaming Translation from Ollama
async function translateTextStream(text, targetLang, modelName, onChunk) {
  currentAbortController = new AbortController();

  const systemPrompt = `Você é um tradutor técnico e acadêmico profissional e rigoroso.
Traduza o texto fornecido diretamente para o idioma: "${targetLang}".
Regras:
1. Retorne APENAS o conteúdo traduzido em formato Markdown.
2. NÃO adicione introduções, explicações, cumprimentos ou notas.
3. Mantenha fórmulas, termos técnicos e formatação intactos.`;

  const prompt = `Traduza o seguinte texto extraído do documento:\n\n"""\n${text}\n"""`;

  const gpuChoice = gpuSelect ? gpuSelect.value : '0';
  const options = {};
  if (gpuChoice === 'cpu') {
    options.num_gpu = 0;
  } else if (gpuChoice === 'auto') {
    options.num_gpu = 99;
  } else {
    options.num_gpu = 99;
    options.main_gpu = parseInt(gpuChoice, 10);
  }

  const response = await fetch('http://localhost:11434/api/generate', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      model: modelName,
      system: systemPrompt,
      prompt: prompt,
      options: options,
      stream: true,
    }),
    signal: currentAbortController.signal,
  });

  if (!response.ok) {
    throw new Error(`Erro no Ollama: ${response.status} ${response.statusText}`);
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder('utf-8');
  let fullTranslation = '';

  while (true) {
    const { done, value } = await reader.read();
    if (done) break;

    const chunk = decoder.decode(value, { stream: true });
    const lines = chunk.split('\n').filter(l => l.trim().length > 0);

    for (const line of lines) {
      try {
        const json = JSON.parse(line);
        if (json.response) {
          fullTranslation += json.response;
          onChunk(fullTranslation);
        }
      } catch (e) {
        // partial json chunk
      }
    }
  }

  return fullTranslation;
}

function displayTranslation(text, isStreaming = false) {
  noTranslationPlaceholder.classList.add('hidden');
  translationMarkdown.classList.remove('hidden');

  const rawHtml = typeof marked !== 'undefined' ? marked.parse(text) : text;
  translationMarkdown.innerHTML = rawHtml + (isStreaming ? '<span class="cursor-blink"></span>' : '');
}

// Translate Single Paragraph In-Place (Checks Cache)
async function handleTranslateSingleBlock(block, forceRefresh = false) {
  if (isTranslating || !block.text) return;

  const model = modelSelect.value;
  const lang = targetLangSelect.value;
  const blockEl = document.getElementById(block.id);

  // Check cache first if not forced
  if (!forceRefresh) {
    const cached = getFromCache(currentPage, block.id);
    if (cached) {
      block.translation = cached;
      renderOverlayLayer(pageParagraphs[currentPage] || []);
      setStatus('Bloco carregado do Cache 💾', 'bg-emerald-500');
      return;
    }
  }

  try {
    isTranslating = true;
    setStatus(`Traduzindo bloco com ${model}...`, 'bg-amber-500');

    if (blockEl) {
      blockEl.style.backgroundColor = `rgba(15, 23, 42, ${overlayOpacity})`;
      blockEl.style.zIndex = '30';
      blockEl.className = 'overlay-block absolute pointer-events-auto rounded p-2.5 transition-all text-slate-100 text-xs shadow-xl border border-indigo-500/80 backdrop-blur-md';
      blockEl.innerHTML = `<div class="flex items-center gap-2 text-indigo-300 italic"><i class="fa-solid fa-spinner fa-spin text-xs"></i><span>Traduzindo com IA local...</span><span class="cursor-blink"></span></div>`;
    }

    const translation = await translateTextStream(block.text, lang, model, (streamed) => {
      if (blockEl) {
        blockEl.innerHTML = `
          <div class="flex items-center justify-between text-[10px] text-indigo-400 mb-1 select-none border-b border-indigo-500/20 pb-0.5">
            <span class="font-mono">Traduzindo...</span>
          </div>
          <div class="text-[12px] leading-relaxed text-slate-100 font-normal select-text prose prose-invert max-w-none">
            ${marked.parse(streamed)}<span class="cursor-blink"></span>
          </div>
        `;
      }
      displayTranslation(streamed, true);
    });

    block.translation = translation;
    saveToCache(currentPage, block.id, translation);

    if (blockEl) {
      blockEl.style.zIndex = '25';
      blockEl.className = 'overlay-block absolute pointer-events-auto rounded p-2.5 transition-all text-slate-100 text-xs shadow-xl border border-indigo-500/50 backdrop-blur-md';
      blockEl.innerHTML = `
        <div class="flex items-center justify-between text-[10px] text-indigo-400 mb-1 select-none border-b border-indigo-500/20 pb-0.5">
          <span class="font-mono font-medium">Tradução Salva 💾</span>
          <span class="hover:text-white" title="Re-traduzir"><i class="fa-solid fa-rotate-right"></i></span>
        </div>
        <div class="text-[12px] leading-relaxed text-slate-100 font-normal select-text prose prose-invert max-w-none">
          ${marked.parse(translation)}
        </div>
      `;
    }
    setStatus('Bloco traduzido e salvo no Cache!', 'bg-emerald-500');
  } catch (err) {
    console.error(err);
    if (blockEl) {
      blockEl.innerHTML = `<span class="text-xs text-rose-400">Erro ao traduzir: ${err.message}</span>`;
    }
    setStatus('Erro ao traduzir bloco', 'bg-rose-500');
  } finally {
    isTranslating = false;
  }
}

// Translate Entire Current Page (Uses Cache if Available)
async function handleTranslateCurrentPage(forceRefresh = false) {
  if (!pdfDoc || isTranslating) return;

  const model = modelSelect.value;
  const lang = targetLangSelect.value;
  const paragraphs = pageParagraphs[currentPage] || [];

  if (paragraphs.length === 0) {
    alert('Nenhum texto detectado nesta página.');
    return;
  }

  // If page is already cached and user didn't force refresh, restore from cache
  const cachedFull = getFromCache(currentPage, 'full');
  if (!forceRefresh && cachedFull && paragraphs.every(p => p.translation && p.translation.length > 0)) {
    pageTranslations[currentPage] = cachedFull;
    displayTranslation(cachedFull, false);
    renderOverlayLayer(paragraphs);
    setStatus('Página carregada do Cache 💾 (Tradução Instantânea)', 'bg-emerald-500');
    return;
  }

  try {
    isTranslating = true;
    translatePageBtn.disabled = true;
    let accumulatedPageText = '';

    for (let i = 0; i < paragraphs.length; i++) {
      const p = paragraphs[i];

      // Check block cache
      let translation = !forceRefresh ? (p.translation || getFromCache(currentPage, p.id)) : null;

      if (!translation) {
        setStatus(`Traduzindo parágrafo ${i + 1}/${paragraphs.length} (Pág. ${currentPage})...`, 'bg-amber-500');

        const blockEl = document.getElementById(p.id);
        if (blockEl) {
          blockEl.style.backgroundColor = `rgba(15, 23, 42, ${overlayOpacity})`;
          blockEl.style.zIndex = '30';
          blockEl.className = 'overlay-block absolute pointer-events-auto rounded p-2.5 transition-all text-slate-100 text-xs shadow-xl border border-indigo-500/80 backdrop-blur-md';
          blockEl.innerHTML = `<div class="flex items-center gap-2 text-indigo-300 italic"><i class="fa-solid fa-spinner fa-spin text-xs"></i><span>Traduzindo #${i + 1}...</span><span class="cursor-blink"></span></div>`;
        }

        translation = await translateTextStream(p.text, lang, model, (streamed) => {
          if (blockEl) {
            blockEl.innerHTML = `
              <div class="flex items-center justify-between text-[10px] text-indigo-400 mb-1 select-none border-b border-indigo-500/20 pb-0.5">
                <span class="font-mono">#${i + 1} Traduzindo...</span>
              </div>
              <div class="text-[12px] leading-relaxed text-slate-100 font-normal select-text prose prose-invert max-w-none">
                ${marked.parse(streamed)}<span class="cursor-blink"></span>
              </div>
            `;
          }
        });

        saveToCache(currentPage, p.id, translation);
      }

      p.translation = translation;
      accumulatedPageText += `### Parágrafo ${i + 1}\n\n${translation}\n\n`;
      displayTranslation(accumulatedPageText, false);

      const blockEl = document.getElementById(p.id);
      if (blockEl) {
        blockEl.style.zIndex = '25';
        blockEl.className = 'overlay-block absolute pointer-events-auto rounded p-2.5 transition-all text-slate-100 text-xs shadow-xl border border-indigo-500/50 backdrop-blur-md';
        blockEl.innerHTML = `
          <div class="flex items-center justify-between text-[10px] text-indigo-400 mb-1 select-none border-b border-indigo-500/20 pb-0.5">
            <span class="font-mono font-medium">#${i + 1} Tradução <span class="text-[9px] bg-emerald-500/20 text-emerald-300 px-1 rounded">💾 Salvo</span></span>
          </div>
          <div class="text-[12px] leading-relaxed text-slate-100 font-normal select-text prose prose-invert max-w-none">
            ${marked.parse(translation)}
          </div>
        `;
      }
    }

    pageTranslations[currentPage] = accumulatedPageText;
    saveToCache(currentPage, 'full', accumulatedPageText);
    setStatus('Página traduzida e salva no Cache 💾!', 'bg-emerald-500');
  } catch (err) {
    console.error(err);
    setStatus('Erro na tradução', 'bg-rose-500');
  } finally {
    isTranslating = false;
    translatePageBtn.disabled = false;
  }
}

// Translate All Pages (Skips cached pages)
async function handleTranslateAllPages() {
  if (!pdfDoc || isTranslating) return;

  progressBarContainer.classList.remove('hidden');
  const total = totalPages;

  for (let p = 1; p <= total; p++) {
    currentPage = p;
    queueRenderPage(p);
    progressBarFill.style.width = `${((p - 1) / total) * 100}%`;

    const cached = getFromCache(p, 'full');
    if (cached) {
      pageTranslations[p] = cached;
      continue;
    }

    await handleTranslateCurrentPage(false);
  }

  progressBarFill.style.width = '100%';
  setTimeout(() => progressBarContainer.classList.add('hidden'), 1000);
  setStatus('Todas as páginas processadas (Cache atualizado)!', 'bg-emerald-500');
}

// Upload Handler
pdfUpload.addEventListener('change', async (e) => {
  const file = e.target.files[0];
  if (!file) return;

  pdfFilename.textContent = file.name;
  pageTranslations = {};
  pageParagraphs = {};

  const fileReader = new FileReader();
  fileReader.onload = async function() {
    const typedarray = new Uint8Array(this.result);
    try {
      setStatus('Carregando documento...', 'bg-amber-500');
      pdfDoc = await pdfjsLib.getDocument(typedarray).promise;
      totalPages = pdfDoc.numPages;
      totalPageCount.textContent = totalPages;
      currentPage = 1;

      noPdfPlaceholder.classList.add('hidden');
      canvasWrapper.classList.remove('hidden');

      renderPage(currentPage);
      setStatus('Documento carregado', 'bg-emerald-500');
    } catch (err) {
      console.error('Erro ao abrir PDF:', err);
      alert('Erro ao carregar o arquivo PDF: ' + err.message);
    }
  };
  fileReader.readAsArrayBuffer(file);
});

// Event Listeners
prevPageBtn.addEventListener('click', () => {
  if (currentPage > 1) {
    currentPage--;
    queueRenderPage(currentPage);
  }
});

nextPageBtn.addEventListener('click', () => {
  if (currentPage < totalPages) {
    currentPage++;
    queueRenderPage(currentPage);
  }
});

zoomInBtn.addEventListener('click', () => {
  scale += 0.2;
  zoomLevel.textContent = `${Math.round(scale * 100 / 1.2)}%`;
  pageParagraphs[currentPage] = null;
  queueRenderPage(currentPage);
});

zoomOutBtn.addEventListener('click', () => {
  if (scale > 0.6) {
    scale -= 0.2;
    zoomLevel.textContent = `${Math.round(scale * 100 / 1.2)}%`;
    pageParagraphs[currentPage] = null;
    queueRenderPage(currentPage);
  }
});

fitWidthBtn.addEventListener('click', () => {
  scale = 1.2;
  zoomLevel.textContent = '100%';
  pageParagraphs[currentPage] = null;
  queueRenderPage(currentPage);
});

toggleOverlayCheckbox.addEventListener('change', () => {
  renderOverlayLayer(pageParagraphs[currentPage] || []);
});

overlayOpacitySlider.addEventListener('input', (e) => {
  overlayOpacity = e.target.value / 100;
  renderOverlayLayer(pageParagraphs[currentPage] || []);
});

modeSplitBtn.addEventListener('click', () => {
  translationSidebar.classList.remove('hidden');
  modeSplitBtn.className = 'px-2 py-0.5 rounded bg-indigo-600 text-white font-medium shadow-sm';
  modeOverlayBtn.className = 'px-2 py-0.5 rounded text-slate-300 hover:text-white';
});

modeOverlayBtn.addEventListener('click', () => {
  modeOverlayBtn.className = 'px-2 py-0.5 rounded bg-indigo-600 text-white font-medium shadow-sm';
  modeSplitBtn.className = 'px-2 py-0.5 rounded text-slate-300 hover:text-white';
});

translatePageBtn.addEventListener('click', () => handleTranslateCurrentPage(false));
translateAllBtn.addEventListener('click', handleTranslateAllPages);
refreshModelsBtn.addEventListener('click', loadOllamaModels);

copyTranslationBtn.addEventListener('click', () => {
  const text = pageTranslations[currentPage] || translationMarkdown.innerText;
  if (text) {
    navigator.clipboard.writeText(text);
    setStatus('Tradução copiada para a área de transferência!', 'bg-indigo-500');
    setTimeout(() => setStatus('Pronto', 'bg-emerald-500'), 2000);
  }
});

clearTranslationBtn.addEventListener('click', () => {
  delete pageTranslations[currentPage];
  clearPageCache(currentPage);
  if (pageParagraphs[currentPage]) {
    pageParagraphs[currentPage].forEach(p => p.translation = '');
    renderOverlayLayer(pageParagraphs[currentPage]);
  }
  translationMarkdown.innerHTML = '';
  translationMarkdown.classList.add('hidden');
  noTranslationPlaceholder.classList.remove('hidden');
  setStatus('Tradução e Cache da página limpos!', 'bg-slate-500');
});

exportMdBtn.addEventListener('click', () => {
  // Collect all translated pages from memory or cache
  const pages = [];
  for (let p = 1; p <= totalPages; p++) {
    const text = pageTranslations[p] || getFromCache(p, 'full');
    if (text) {
      pages.push({ page: p, text });
    }
  }

  if (pages.length === 0) {
    alert('Nenhuma tradução salva no cache para exportar.');
    return;
  }

  let fullDoc = `# Tradução do Documento: ${pdfFilename.textContent}\n\n`;
  pages.forEach(p => {
    fullDoc += `## Página ${p.page}\n\n${p.text}\n\n---\n\n`;
  });

  const blob = new Blob([fullDoc], { type: 'text/markdown;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${pdfFilename.textContent || 'documento'}_traduzido.md`;
  a.click();
  URL.revokeObjectURL(url);
});

// Discover Ollama models on start
loadOllamaModels();
