/**
 * ErrinILS - Universal Website Scripts
 * Clean, lightweight, zero-dependency client scripts.
 * Features:
 * - Theme Switcher (Light / Dark mode persisted in localStorage)
 * - Real Application Screenshot Viewer (interactive tabs & keyboard support)
 * - Mobile Navigation Toggle
 * - Code Block Copying
 * - User Guide Downloader (.pdf, .md, .txt) & Print
 * - Sticky Table of Contents Scroll-Spy
 */

document.addEventListener('DOMContentLoaded', function () {
  initThemeToggle();
  initMobileNav();
  initScreenshotViewer();
  initCodeCopyButtons();
  initGuideActions();
  initScrollSpy();
});

/* -------------------------------------------------------------------
   1. Theme Switcher (Light / Dark)
   ------------------------------------------------------------------- */
function initThemeToggle() {
  var toggles = document.querySelectorAll('.theme-toggle-btn');
  
  function applyTheme(theme) {
    document.documentElement.setAttribute('data-theme', theme);
    try {
      localStorage.setItem('errin_theme', theme);
    } catch (e) {}
    updateToggleIcons(theme);
  }

  function updateToggleIcons(theme) {
    toggles.forEach(function (btn) {
      if (theme === 'dark') {
        btn.setAttribute('aria-label', 'Switch to light mode');
        btn.setAttribute('title', 'Switch to light mode');
        btn.innerHTML = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="5"></circle><line x1="12" y1="1" x2="12" y2="3"></line><line x1="12" y1="21" x2="12" y2="23"></line><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line><line x1="1" y1="12" x2="3" y2="12"></line><line x1="21" y1="12" x2="23" y2="12"></line><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line></svg>';
      } else {
        btn.setAttribute('aria-label', 'Switch to dark mode');
        btn.setAttribute('title', 'Switch to dark mode');
        btn.innerHTML = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path></svg>';
      }
    });
  }

  var currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
  updateToggleIcons(currentTheme);

  toggles.forEach(function (btn) {
    btn.addEventListener('click', function () {
      var current = document.documentElement.getAttribute('data-theme') || 'light';
      var next = current === 'dark' ? 'light' : 'dark';
      applyTheme(next);
    });
  });
}

/* -------------------------------------------------------------------
   2. Mobile Navigation Toggle
   ------------------------------------------------------------------- */
function initMobileNav() {
  var menuBtn = document.querySelector('.mobile-menu-btn');
  var mainNav = document.querySelector('.main-nav');

  if (menuBtn && mainNav) {
    menuBtn.addEventListener('click', function () {
      mainNav.classList.toggle('open');
      var isExpanded = mainNav.classList.contains('open');
      menuBtn.setAttribute('aria-expanded', isExpanded);
      menuBtn.textContent = isExpanded ? '✕' : '☰';
    });
  }
}

/* -------------------------------------------------------------------
   3. Real Application Screenshot Showcase
   ------------------------------------------------------------------- */
function initScreenshotViewer() {
  var tabs = document.querySelectorAll('.showcase-tab');
  var img = document.getElementById('showcase-image');
  var titleEl = document.getElementById('window-title-text');
  var captionEl = document.getElementById('showcase-caption');

  if (!tabs.length || !img) return;

  var shots = {
    circulation: {
      src: 'assets/screenshot-circulation.png',
      alt: 'ErrinILS Circulation Desk Screen',
      title: 'ErrinILS — Circulation Desk',
      caption: '<strong>Circulation Desk:</strong> Fast barcode scanning for Malaysian IC student cards and book barcodes. Shows active loans, loan limits (default 3 books / 7 days), hold queues, and fine status immediately.'
    },
    catalog: {
      src: 'assets/screenshot-catalog.png',
      alt: 'ErrinILS ISBN Copy-Cataloguing Screen',
      title: 'ErrinILS — Catalog & Lookup',
      caption: '<strong>5-Source ISBN Copy-Cataloguing:</strong> Scan or type any ISBN. ErrinILS automatically queries the Library of Congress (via native Z39.50) and Open Library to fill in title, author, publisher, and Dewey Decimal classification.'
    },
    simple: {
      src: 'assets/screenshot-kiosk.png',
      alt: 'ErrinILS Simplified View Screen',
      title: 'ErrinILS — Simplified View',
      caption: '<strong>Simplified View:</strong> Simplified, touch-friendly circulation screen designed for quick checkout and return. Large buttons, clear status banners, and friendly error dialogs so students can borrow and return books independently.'
    },
    reports: {
      src: 'assets/screenshot-reports.png',
      alt: 'ErrinILS Reports and Reading Champions Screen',
      title: 'ErrinILS — Reports & Analytics',
      caption: '<strong>Reports & Analytics:</strong> Instant reading statistics including the Nilam Reading Champions leaderboard, 6-month borrowing trends, Dewey classification distribution, and 1-click CSV export.'
    },
    students: {
      src: 'assets/screenshot-students.png',
      alt: 'ErrinILS Student & Patron Management Screen',
      title: 'ErrinILS — Patron Management',
      caption: '<strong>Student & Patron Management:</strong> Register students with automatic Malaysian IC / MyKid format validation (<code>YYMMDD-PB-###G</code>). Search by name, IC, or class standard with instant borrowing history.'
    },
    fines: {
      src: 'assets/screenshot-fines.png',
      alt: 'ErrinILS Fines Management Screen',
      title: 'ErrinILS — Fines & Overdues',
      caption: '<strong>Fines & Overdues:</strong> Track overdue books and fine balances per student. Automatically blocks borrowing if overdue items exist or unpaid fines exceed threshold.'
    },
    settings: {
      src: 'assets/screenshot-settings.png',
      alt: 'ErrinILS Settings and Mobile QR Screen',
      title: 'ErrinILS — Settings & Remote Access',
      caption: '<strong>Settings & Mobile QR Access:</strong> Pair phones and tablets over school Wi-Fi with a 6-digit PIN and QR code for wireless shelf audits. One-click ZIP backup of the entire library database.'
    }
  };

  var currentIndex = 0;
  var tabKeys = Object.keys(shots);

  function selectTab(key) {
    var data = shots[key];
    if (!data) return;

    tabs.forEach(function (tab) {
      if (tab.getAttribute('data-shot') === key) {
        tab.classList.add('active');
        tab.setAttribute('aria-selected', 'true');
      } else {
        tab.classList.remove('active');
        tab.setAttribute('aria-selected', 'false');
      }
    });

    img.style.opacity = '0.4';
    setTimeout(function () {
      img.src = data.src;
      img.alt = data.alt;
      img.style.opacity = '1';
    }, 100);

    if (titleEl) titleEl.textContent = data.title;
    if (captionEl) captionEl.innerHTML = data.caption;
  }

  tabs.forEach(function (tab, index) {
    tab.addEventListener('click', function () {
      var key = tab.getAttribute('data-shot');
      currentIndex = index;
      selectTab(key);
    });
  });

  // Keyboard navigation across tabs
  document.addEventListener('keydown', function (e) {
    if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'TEXTAREA')) {
      return;
    }
    if (e.key === 'ArrowRight') {
      currentIndex = (currentIndex + 1) % tabKeys.length;
      selectTab(tabKeys[currentIndex]);
    } else if (e.key === 'ArrowLeft') {
      currentIndex = (currentIndex - 1 + tabKeys.length) % tabKeys.length;
      selectTab(tabKeys[currentIndex]);
    }
  });
}

/* -------------------------------------------------------------------
   4. Code Copy Buttons
   ------------------------------------------------------------------- */
function initCodeCopyButtons() {
  var copyButtons = document.querySelectorAll('.code-box-copy');
  copyButtons.forEach(function (btn) {
    btn.addEventListener('click', function () {
      var codeBox = btn.closest('.code-box');
      if (!codeBox) return;
      var pre = codeBox.querySelector('pre');
      if (!pre) return;

      var text = pre.innerText || pre.textContent;
      navigator.clipboard.writeText(text).then(function () {
        var originalText = btn.textContent;
        btn.textContent = 'Copied';
        setTimeout(function () {
          btn.textContent = originalText;
        }, 2000);
      }).catch(function () {
        btn.textContent = 'Error';
      });
    });
  });
}

/* -------------------------------------------------------------------
   5. User Guide Actions (PDF, MD, TXT Download & Print)
   ------------------------------------------------------------------- */
function initGuideActions() {
  var btnDownloadPdf = document.getElementById('btn-download-guide-pdf');
  var btnDownloadMd = document.getElementById('btn-download-guide-md');
  var btnDownloadTxt = document.getElementById('btn-download-guide-txt');
  var btnPrintGuide = document.getElementById('btn-print-guide');

  var pdfPath = 'assets/ErrinILS-User-Guide.pdf';
  var guidePath = 'assets/ErrinILS-User-Guide.md';

  if (btnDownloadPdf) {
    btnDownloadPdf.addEventListener('click', function () {
      window.location.href = pdfPath;
    });
  }

  function triggerBlobDownload(content, filename, mimeType) {
    var blob = new Blob([content], { type: mimeType });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  if (btnDownloadMd) {
    btnDownloadMd.addEventListener('click', function (e) {
      e.preventDefault();
      fetch(guidePath)
        .then(function (res) {
          if (!res.ok) throw new Error('File not found');
          return res.text();
        })
        .then(function (mdText) {
          triggerBlobDownload(mdText, 'ErrinILS-User-Guide-v1.3.2.md', 'text/markdown;charset=utf-8');
        })
        .catch(function () {
          window.location.href = guidePath;
        });
    });
  }

  if (btnDownloadTxt) {
    btnDownloadTxt.addEventListener('click', function (e) {
      e.preventDefault();
      fetch(guidePath)
        .then(function (res) {
          if (!res.ok) throw new Error('File not found');
          return res.text();
        })
        .then(function (text) {
          triggerBlobDownload(text, 'ErrinILS-User-Guide-v1.3.2.txt', 'text/plain;charset=utf-8');
        })
        .catch(function () {
          window.location.href = guidePath;
        });
    });
  }

  if (btnPrintGuide) {
    btnPrintGuide.addEventListener('click', function () {
      window.print();
    });
  }
}

/* -------------------------------------------------------------------
   6. Sticky Scroll-Spy for Table of Contents
   ------------------------------------------------------------------- */
function initScrollSpy() {
  var chapters = document.querySelectorAll('.guide-chapter');
  var navLinks = document.querySelectorAll('.guide-nav-link');

  if (!chapters.length || !navLinks.length) return;

  function onScroll() {
    var scrollPos = window.scrollY + 120;
    var currentChapterId = '';

    chapters.forEach(function (ch) {
      var top = ch.offsetTop;
      var height = ch.offsetHeight;
      if (scrollPos >= top && scrollPos < top + height) {
        currentChapterId = ch.getAttribute('id');
      }
    });

    if (currentChapterId) {
      navLinks.forEach(function (link) {
        var href = link.getAttribute('href');
        if (href === '#' + currentChapterId) {
          link.classList.add('active');
        } else {
          link.classList.remove('active');
        }
      });
    }
  }

  window.addEventListener('scroll', onScroll, { passive: true });
  onScroll();
}
