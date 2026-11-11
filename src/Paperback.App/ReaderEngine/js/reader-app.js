// Paperback Reader Engine App
let pageFlip = null;
let currentBook = null;
let pdfDoc = null;
let currentTheme = 'sepia';
let currentTypography = {
    fontFamily: 'Georgia',
    fontSize: 18,
    lineHeight: 1.6,
    marginRatio: 1.0,
    justifyText: true
};

// Initialize PDF.js worker
if (window.pdfjsLib) {
    window.pdfjsLib.GlobalWorkerOptions.workerSrc = 'js/pdf.worker.min.js';
}

function postToHost(msg) {
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(msg);
    } else {
        console.log('[Host Bridge]', msg);
    }
}

function showLoading(text) {
    const overlay = document.getElementById('loading-overlay');
    const loadingText = overlay.querySelector('.loading-text');
    if (loadingText && text) loadingText.textContent = text;
    overlay.classList.remove('hidden');
}

function hideLoading() {
    const overlay = document.getElementById('loading-overlay');
    overlay.classList.add('hidden');
}

function calculateDimensions() {
    const vWidth = window.innerWidth;
    const vHeight = window.innerHeight;
    const isSpread = vWidth >= 900;

    // Available reading area leaving padding for window borders
    const availWidth = Math.max(300, vWidth - 40);
    const availHeight = Math.max(400, vHeight - 40);

    let pageW, pageH;
    if (isSpread) {
        pageW = Math.floor(availWidth / 2);
        pageH = Math.floor(Math.min(availHeight, pageW * 1.45));
    } else {
        pageW = Math.floor(Math.min(availWidth, availHeight * 0.7));
        pageH = Math.floor(Math.min(availHeight, pageW * 1.45));
    }

    return { pageW, pageH, isSpread };
}

function initPageFlip(pageW, pageH, isSpread) {
    if (pageFlip) {
        try { pageFlip.destroy(); } catch (e) { }
        pageFlip = null;
    }

    const bookElem = document.getElementById('book');
    bookElem.innerHTML = '';

    pageFlip = new St.PageFlip(bookElem, {
        width: pageW,
        height: pageH,
        size: 'fixed',
        minWidth: 300,
        maxWidth: 1400,
        minHeight: 400,
        maxHeight: 1800,
        drawShadow: true,
        maxShadowOpacity: 0.35,
        showCover: false,
        usePortrait: !isSpread,
        startZIndex: 0,
        autoSize: true,
        flippingTime: 380,
        useMouseEvents: true,
        swipeDistance: 30
    });

    pageFlip.on('flip', (e) => {
        const pageIdx = e.data; // 0-based
        const total = pageFlip.getPageCount();
        const curPage = pageIdx + 1;
        const percent = total > 0 ? (curPage / total) * 100.0 : 0.0;

        postToHost({
            type: 'pageChanged',
            currentPage: curPage,
            totalPages: total,
            progressPercent: percent
        });
    });

    return pageFlip;
}

// Render PDF Document
async function loadPdfDocument(fileUrl, targetStartPage) {
    showLoading('Rendering PDF pages...');
    try {
        const loadingTask = pdfjsLib.getDocument({ url: fileUrl });
        pdfDoc = await loadingTask.promise;
        const totalPages = pdfDoc.numPages;

        const { pageW, pageH, isSpread } = calculateDimensions();
        const flip = initPageFlip(pageW, pageH, isSpread);

        const bookElem = document.getElementById('book');
        bookElem.innerHTML = '';

        // Create page elements
        const pageElements = [];
        for (let i = 1; i <= totalPages; i++) {
            const pageDiv = document.createElement('div');
            pageDiv.className = 'page';
            
            const container = document.createElement('div');
            container.className = 'pdf-page-container';

            const canvas = document.createElement('canvas');
            canvas.id = `pdf-canvas-${i}`;
            container.appendChild(canvas);
            pageDiv.appendChild(container);
            bookElem.appendChild(pageDiv);
            pageElements.push(pageDiv);
        }

        // Render first few pages immediately, lazy-load remaining
        const dpr = Math.min(window.devicePixelRatio || 1, 2.0);
        for (let i = 1; i <= Math.min(totalPages, 6); i++) {
            await renderPdfPage(i, pageW, pageH, dpr);
        }

        flip.loadFromHTML(bookElem.querySelectorAll('.page'));

        if (targetStartPage && targetStartPage > 1) {
            const safePage = Math.min(targetStartPage - 1, totalPages - 1);
            flip.turnToPage(safePage);
        }

        hideLoading();
        postToHost({
            type: 'bookLoaded',
            totalPages: totalPages
        });

        // Background render rest of the pages
        setTimeout(async () => {
            for (let i = 7; i <= totalPages; i++) {
                await renderPdfPage(i, pageW, pageH, dpr);
            }
        }, 300);

    } catch (err) {
        console.error('Failed to load PDF:', err);
        showLoading('Error loading PDF: ' + err.message);
    }
}

async function renderPdfPage(pageNum, pageW, pageH, dpr) {
    if (!pdfDoc) return;
    try {
        const page = await pdfDoc.getPage(pageNum);
        const canvas = document.getElementById(`pdf-canvas-${pageNum}`);
        if (!canvas) return;

        const unscaledViewport = page.getViewport({ scale: 1.0 });
        const scaleX = pageW / unscaledViewport.width;
        const scaleY = pageH / unscaledViewport.height;
        const scale = Math.min(scaleX, scaleY);

        const viewport = page.getViewport({ scale: scale * dpr });
        canvas.width = viewport.width;
        canvas.height = viewport.height;
        canvas.style.width = `${viewport.width / dpr}px`;
        canvas.style.height = `${viewport.height / dpr}px`;

        const ctx = canvas.getContext('2d');
        const renderContext = {
            canvasContext: ctx,
            viewport: viewport
        };
        await page.render(renderContext).promise;
    } catch (e) {
        console.warn(`Render error page ${pageNum}:`, e);
    }
}

// Render EPUB Chapters
function loadEpubContent(bookData, targetStartPage) {
    showLoading('Formatting eBook pages...');
    try {
        const { pageW, pageH, isSpread } = calculateDimensions();
        const flip = initPageFlip(pageW, pageH, isSpread);

        const bookElem = document.getElementById('book');
        bookElem.innerHTML = '';

        const chapters = bookData.chapters || [];
        const pages = [];

        // Paginate chapters
        let globalPage = 1;
        chapters.forEach((chap, cIdx) => {
            const chapTitle = chap.title || `Chapter ${cIdx + 1}`;
            const chapText = chap.text || chap.html || '';

            // Split into paragraphs
            const paras = chapText.split(/\n\s*\n/).filter(p => p.trim().length > 0);
            
            // Group paragraphs into pages based on height / character volume
            let currentParas = [];
            let currentChars = 0;
            const charsPerPage = isSpread ? 1100 : 900;

            paras.forEach((para) => {
                currentParas.push(para);
                currentChars += para.length;
                if (currentChars >= charsPerPage) {
                    pages.push({
                        chapterTitle: chapTitle,
                        html: currentParas.map(p => `<p>${escapeHtml(p)}</p>`).join(''),
                        pageNum: globalPage++
                    });
                    currentParas = [];
                    currentChars = 0;
                }
            });

            if (currentParas.length > 0) {
                pages.push({
                    chapterTitle: chapTitle,
                    html: currentParas.map(p => `<p>${escapeHtml(p)}</p>`).join(''),
                    pageNum: globalPage++
                });
            }
        });

        if (pages.length === 0) {
            pages.push({
                chapterTitle: bookData.title || 'Untitled',
                html: `<p>${escapeHtml(bookData.description || 'Enjoy reading!')}</p>`,
                pageNum: 1
            });
        }

        const totalPages = pages.length;

        // Build DOM elements for each page
        pages.forEach((p) => {
            const pageDiv = document.createElement('div');
            pageDiv.className = 'page';

            const content = document.createElement('div');
            content.className = 'page-content';
            applyTypographyToElement(content);

            const header = document.createElement('div');
            header.className = 'page-header';
            header.textContent = p.chapterTitle;

            const body = document.createElement('div');
            body.className = 'page-body';
            body.innerHTML = p.html;

            const footer = document.createElement('div');
            footer.className = 'page-footer';
            footer.textContent = p.pageNum;

            content.appendChild(header);
            content.appendChild(body);
            content.appendChild(footer);
            pageDiv.appendChild(content);

            bookElem.appendChild(pageDiv);
        });

        flip.loadFromHTML(bookElem.querySelectorAll('.page'));

        if (targetStartPage && targetStartPage > 1) {
            const safePage = Math.min(targetStartPage - 1, totalPages - 1);
            flip.turnToPage(safePage);
        }

        hideLoading();
        postToHost({
            type: 'bookLoaded',
            totalPages: totalPages
        });

    } catch (err) {
        console.error('Failed to load EPUB:', err);
        showLoading('Error formatting book: ' + err.message);
    }
}

function escapeHtml(text) {
    const div = document.createElement('div');
    div.innerText = text;
    return div.innerHTML;
}

function applyTypographyToElement(elem) {
    if (!elem) return;
    elem.style.fontFamily = currentTypography.fontFamily + ', serif';
    elem.style.fontSize = `${currentTypography.fontSize}px`;
    elem.style.lineHeight = currentTypography.lineHeight;
    elem.style.textAlign = currentTypography.justifyText ? 'justify' : 'left';
    elem.style.padding = `${Math.round(40 * currentTypography.marginRatio)}px ${Math.round(48 * currentTypography.marginRatio)}px`;
}

function setTheme(theme) {
    currentTheme = theme.toLowerCase();
    document.body.className = `theme-${currentTheme}`;
}

function setTypography(typo) {
    currentTypography = Object.assign(currentTypography, typo);
    const contents = document.querySelectorAll('.page-content');
    contents.forEach(applyTypographyToElement);
}

// Window resizing
let resizeTimeout = null;
window.addEventListener('resize', () => {
    clearTimeout(resizeTimeout);
    resizeTimeout = setTimeout(() => {
        if (pageFlip) {
            const { pageW, pageH } = calculateDimensions();
            pageFlip.updateFromHtml(document.querySelectorAll('.page'));
        }
    }, 250);
});

// Click in middle region triggers distraction-free toggle
window.addEventListener('click', (e) => {
    const w = window.innerWidth;
    const x = e.clientX;
    // If click is in the middle 40% of the screen
    if (x > w * 0.3 && x < w * 0.7) {
        postToHost({ type: 'toggleUi' });
    }
});

// Keyboard navigation
window.addEventListener('keydown', (e) => {
    if (!pageFlip) return;
    if (e.key === 'ArrowRight' || e.key === 'PageDown' || e.key === ' ') {
        pageFlip.flipNext();
    } else if (e.key === 'ArrowLeft' || e.key === 'PageUp') {
        pageFlip.flipPrev();
    }
});

// WebView2 message listener from C# WPF host
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
        const msg = event.data;
        if (!msg) return;

        switch (msg.action) {
            case 'loadBook':
                currentBook = msg.book;
                if (msg.book.format === 'PDF') {
                    loadPdfDocument(msg.book.fileUrl, msg.book.startPage || 1);
                } else {
                    loadEpubContent(msg.book, msg.book.startPage || 1);
                }
                break;

            case 'nextPage':
                if (pageFlip) pageFlip.flipNext();
                break;

            case 'prevPage':
                if (pageFlip) pageFlip.flipPrev();
                break;

            case 'jumpToPage':
                if (pageFlip && typeof msg.page === 'number') {
                    const idx = Math.max(0, Math.min(msg.page - 1, pageFlip.getPageCount() - 1));
                    pageFlip.turnToPage(idx);
                }
                break;

            case 'setTheme':
                if (msg.theme) setTheme(msg.theme);
                break;

            case 'setTypography':
                if (msg.typography) setTypography(msg.typography);
                break;
        }
    });
}

// Notify host that the reader engine is ready
window.addEventListener('DOMContentLoaded', () => {
    postToHost({ type: 'ready' });
});
