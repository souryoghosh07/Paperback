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

let autoHideTimeout = null;
let isPinned = false;
let isPopoverOpen = false;
let isDrawerOpen = false;

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

// ==================== AUTO-HIDE CHROME ====================
function showChrome() {
    const topBar = document.getElementById('top-bar');
    const bottomBar = document.getElementById('bottom-bar');
    if (topBar) topBar.classList.remove('hidden');
    if (bottomBar) bottomBar.classList.remove('hidden');
    resetAutoHideTimer();
}

function hideChrome() {
    if (isPinned || isPopoverOpen || isDrawerOpen) return;
    const topBar = document.getElementById('top-bar');
    const bottomBar = document.getElementById('bottom-bar');
    if (topBar) topBar.classList.add('hidden');
    if (bottomBar) bottomBar.classList.add('hidden');
}

function resetAutoHideTimer() {
    clearTimeout(autoHideTimeout);
    if (!isPinned && !isPopoverOpen && !isDrawerOpen) {
        autoHideTimeout = setTimeout(hideChrome, 4500);
    }
}

// Mouse movement inside window reveals toolbars
window.addEventListener('mousemove', () => {
    showChrome();
});

// ==================== DIMENSIONS & FLIP INITIALIZATION ====================
function calculateDimensions() {
    const vWidth = window.innerWidth;
    const vHeight = window.innerHeight;
    const isSpread = vWidth >= 900;

    const availWidth = Math.max(300, vWidth - 40);
    const availHeight = Math.max(400, vHeight - 80);

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

        updateProgressUi(curPage, total, percent);

        postToHost({
            type: 'pageChanged',
            currentPage: curPage,
            totalPages: total,
            progressPercent: percent
        });
    });

    return pageFlip;
}

function updateProgressUi(current, total, percent) {
    const slider = document.getElementById('page-slider');
    const text = document.getElementById('page-progress-text');
    if (slider) {
        slider.max = Math.max(1, total);
        slider.value = current;
    }
    if (text) {
        text.textContent = `Page ${current} of ${total} (${Math.round(percent)}%)`;
    }
}

// ==================== PDF LOADER ====================
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
        }

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
        updateProgressUi(targetStartPage || 1, totalPages, ((targetStartPage || 1) / totalPages) * 100);

        postToHost({
            type: 'bookLoaded',
            totalPages: totalPages
        });

        // Lazy-render remaining pages
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
        await page.render({
            canvasContext: ctx,
            viewport: viewport
        }).promise;
    } catch (e) {
        console.warn(`Render error page ${pageNum}:`, e);
    }
}

// ==================== EPUB LOADER ====================
function loadEpubContent(bookData, targetStartPage) {
    showLoading('Formatting eBook pages...');
    try {
        const { pageW, pageH, isSpread } = calculateDimensions();
        const flip = initPageFlip(pageW, pageH, isSpread);

        const bookElem = document.getElementById('book');
        bookElem.innerHTML = '';

        const chapters = bookData.chapters || [];
        const pages = [];

        let globalPage = 1;
        chapters.forEach((chap, cIdx) => {
            const chapTitle = chap.title || `Chapter ${cIdx + 1}`;
            const chapText = chap.text || chap.html || '';

            const paras = chapText.split(/\n\s*\n/).filter(p => p.trim().length > 0);
            
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
        updateProgressUi(targetStartPage || 1, totalPages, ((targetStartPage || 1) / totalPages) * 100);

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
    elem.style.fontFamily = currentTypography.fontFamily;
    elem.style.fontSize = `${currentTypography.fontSize}px`;
    elem.style.lineHeight = currentTypography.lineHeight;
    elem.style.textAlign = currentTypography.justifyText ? 'justify' : 'left';
    elem.style.padding = `${Math.round(40 * currentTypography.marginRatio)}px ${Math.round(48 * currentTypography.marginRatio)}px`;
}

function setTheme(theme) {
    currentTheme = theme.toLowerCase();
    document.body.className = `theme-${currentTheme}`;
    document.querySelectorAll('.theme-btn').forEach(btn => {
        btn.classList.toggle('active', btn.dataset.theme === currentTheme);
    });
}

function setTypography(typo) {
    currentTypography = Object.assign(currentTypography, typo);
    const contents = document.querySelectorAll('.page-content');
    contents.forEach(applyTypographyToElement);
}

// ==================== UI CONTROLS & EVENT LISTENERS ====================
document.addEventListener('DOMContentLoaded', () => {
    // Top Bar Buttons
    const btnLibrary = document.getElementById('btn-library');
    if (btnLibrary) {
        btnLibrary.addEventListener('click', (e) => {
            e.stopPropagation();
            postToHost({ type: 'closeReader' });
        });
    }

    const btnFullscreen = document.getElementById('btn-fullscreen');
    if (btnFullscreen) {
        btnFullscreen.addEventListener('click', (e) => {
            e.stopPropagation();
            postToHost({ type: 'toggleFullscreen' });
        });
    }

    const btnBookmark = document.getElementById('btn-bookmark');
    if (btnBookmark) {
        btnBookmark.addEventListener('click', (e) => {
            e.stopPropagation();
            const curPage = pageFlip ? pageFlip.getCurrentPageIndex() + 1 : 1;
            postToHost({ type: 'addBookmark', page: curPage });
            btnBookmark.style.color = '#007AFF';
            setTimeout(() => { btnBookmark.style.color = ''; }, 1200);
        });
    }

    // Typography Popover Toggle
    const btnTypography = document.getElementById('btn-typography');
    const popover = document.getElementById('typography-popover');
    if (btnTypography && popover) {
        btnTypography.addEventListener('click', (e) => {
            e.stopPropagation();
            closeDrawer();
            isPopoverOpen = !isPopoverOpen;
            popover.classList.toggle('hidden', !isPopoverOpen);
            resetAutoHideTimer();
        });
    }

    // Table of Contents Drawer Toggle
    const btnToc = document.getElementById('btn-toc');
    const drawer = document.getElementById('toc-drawer');
    const btnCloseToc = document.getElementById('btn-close-toc');
    if (btnToc && drawer) {
        btnToc.addEventListener('click', (e) => {
            e.stopPropagation();
            closePopover();
            isDrawerOpen = !isDrawerOpen;
            drawer.classList.toggle('hidden', !isDrawerOpen);
            resetAutoHideTimer();
        });
    }
    if (btnCloseToc) {
        btnCloseToc.addEventListener('click', closeDrawer);
    }

    // Theme Switchers
    document.querySelectorAll('.theme-btn').forEach(btn => {
        btn.addEventListener('click', (e) => {
            e.stopPropagation();
            const theme = btn.dataset.theme;
            setTheme(theme);
        });
    });

    // Font Selector
    const fontSelect = document.getElementById('font-select');
    if (fontSelect) {
        fontSelect.addEventListener('change', (e) => {
            setTypography({ fontFamily: e.target.value });
        });
    }

    // Font Size Controls
    const fontSlider = document.getElementById('font-size-slider');
    const btnFontDecrease = document.getElementById('btn-font-decrease');
    const btnFontIncrease = document.getElementById('btn-font-increase');

    if (fontSlider) {
        fontSlider.addEventListener('input', (e) => {
            setTypography({ fontSize: parseInt(e.target.value, 10) });
        });
    }
    if (btnFontDecrease && fontSlider) {
        btnFontDecrease.addEventListener('click', () => {
            let val = Math.max(13, parseInt(fontSlider.value, 10) - 1);
            fontSlider.value = val;
            setTypography({ fontSize: val });
        });
    }
    if (btnFontIncrease && fontSlider) {
        btnFontIncrease.addEventListener('click', () => {
            let val = Math.min(32, parseInt(fontSlider.value, 10) + 1);
            fontSlider.value = val;
            setTypography({ fontSize: val });
        });
    }

    // Justify / Align Left
    const btnJustify = document.getElementById('btn-justify');
    const btnAlignLeft = document.getElementById('btn-align-left');
    if (btnJustify && btnAlignLeft) {
        btnJustify.addEventListener('click', () => {
            btnJustify.classList.add('active');
            btnAlignLeft.classList.remove('active');
            setTypography({ justifyText: true });
        });
        btnAlignLeft.addEventListener('click', () => {
            btnAlignLeft.classList.add('active');
            btnJustify.classList.remove('active');
            setTypography({ justifyText: false });
        });
    }

    // Pin Toolbar Toggle
    const chkPin = document.getElementById('chk-pin-toolbar');
    if (chkPin) {
        chkPin.addEventListener('change', (e) => {
            isPinned = e.target.checked;
            showChrome();
        });
    }

    // Bottom Bar Navigation Buttons
    const btnPrev = document.getElementById('btn-prev');
    const btnNext = document.getElementById('btn-next');
    if (btnPrev) btnPrev.addEventListener('click', () => { if (pageFlip) pageFlip.flipPrev(); });
    if (btnNext) btnNext.addEventListener('click', () => { if (pageFlip) pageFlip.flipNext(); });

    // Scrubber Slider
    const pageSlider = document.getElementById('page-slider');
    if (pageSlider) {
        pageSlider.addEventListener('input', (e) => {
            if (pageFlip) {
                const target = parseInt(e.target.value, 10) - 1;
                pageFlip.turnToPage(target);
            }
        });
    }

    // Close overlays when clicking outside
    document.addEventListener('click', (e) => {
        if (isPopoverOpen && !e.target.closest('#typography-popover') && !e.target.closest('#btn-typography')) {
            closePopover();
        }
        if (isDrawerOpen && !e.target.closest('#toc-drawer') && !e.target.closest('#btn-toc')) {
            closeDrawer();
        }
    });

    // Notify C# host that DOM is ready
    postToHost({ type: 'ready' });
    showChrome();
});

function closePopover() {
    isPopoverOpen = false;
    const popover = document.getElementById('typography-popover');
    if (popover) popover.classList.add('hidden');
    resetAutoHideTimer();
}

function closeDrawer() {
    isDrawerOpen = false;
    const drawer = document.getElementById('toc-drawer');
    if (drawer) drawer.classList.add('hidden');
    resetAutoHideTimer();
}

function renderTableOfContents(toc) {
    const list = document.getElementById('toc-list');
    if (!list) return;
    list.innerHTML = '';
    if (!toc || toc.length === 0) {
        list.innerHTML = '<div style="opacity:0.5; padding:12px;">No chapters found</div>';
        return;
    }

    toc.forEach(item => {
        const row = document.createElement('div');
        row.className = 'toc-item';
        row.innerHTML = `<span>${escapeHtml(item.title || 'Untitled')}</span><span class="toc-page">p. ${item.pageNumber || 1}</span>`;
        row.addEventListener('click', () => {
            if (pageFlip && typeof item.pageNumber === 'number') {
                pageFlip.turnToPage(Math.max(0, item.pageNumber - 1));
                closeDrawer();
            }
        });
        list.appendChild(row);
    });
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

// Middle-click toggles toolbar visibility
window.addEventListener('click', (e) => {
    if (e.target.closest('.chrome-bar') || e.target.closest('.popover') || e.target.closest('.drawer')) {
        return;
    }
    const w = window.innerWidth;
    const x = e.clientX;
    if (x > w * 0.3 && x < w * 0.7) {
        const topBar = document.getElementById('top-bar');
        if (topBar && topBar.classList.contains('hidden')) {
            showChrome();
        } else {
            hideChrome();
        }
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

// C# WPF Host Message Receiver
if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', (event) => {
        const msg = event.data;
        if (!msg) return;

        switch (msg.action) {
            case 'loadBook':
                currentBook = msg.book;
                const titleElem = document.getElementById('book-title');
                const authorElem = document.getElementById('book-author');
                if (titleElem) titleElem.textContent = msg.book.title || 'Paperback';
                if (authorElem) authorElem.textContent = msg.book.author || '';

                if (msg.book.toc) {
                    renderTableOfContents(msg.book.toc);
                }

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
