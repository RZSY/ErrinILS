/**
 * ErrinILS - Universal Website Scripts
 * Features:
 * - Interactive Hero UI Slideshow (autoplay, controls, pause-on-hover)
 * - Mobile Navigation Drawer Toggle
 * - Code Block Clipboard Copying
 * - User Guide Downloader (.md and .txt) and Print trigger
 * - Sticky Table of Contents Scroll-Spy
 */

document.addEventListener('DOMContentLoaded', function () {
  initMobileNav();
  initSlideshow();
  initCodeCopyButtons();
  initGuideActions();
  initScrollSpy();
});

/* -------------------------------------------------------------------
   1. Mobile Navigation Toggle
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
   2. Hero Slideshow Engine
   ------------------------------------------------------------------- */
function initSlideshow() {
  var wrapper = document.querySelector('.slideshow-wrapper');
  if (!wrapper) return;

  var slides = wrapper.querySelectorAll('.slide');
  var dots = wrapper.querySelectorAll('.slide-dot');
  var prevBtn = wrapper.querySelector('.slide-prev');
  var nextBtn = wrapper.querySelector('.slide-next');
  var playStateLabel = wrapper.querySelector('.slide-play-state');
  
  if (!slides.length) return;

  var currentIndex = 0;
  var totalSlides = slides.length;
  var timer = null;
  var isPlaying = true;
  var autoIntervalMs = 5000;

  function showSlide(index) {
    if (index >= totalSlides) currentIndex = 0;
    else if (index < 0) currentIndex = totalSlides - 1;
    else currentIndex = index;

    slides.forEach(function (slide, idx) {
      if (idx === currentIndex) {
        slide.classList.add('active');
      } else {
        slide.classList.remove('active');
      }
    });

    dots.forEach(function (dot, idx) {
      if (idx === currentIndex) {
        dot.classList.add('active');
        dot.setAttribute('aria-current', 'true');
      } else {
        dot.classList.remove('active');
        dot.removeAttribute('aria-current');
      }
    });

    if (playStateLabel) {
      playStateLabel.textContent = 'Slide ' + (currentIndex + 1) + ' of ' + totalSlides;
    }
  }

  function startAutoplay() {
    stopAutoplay();
    isPlaying = true;
    timer = setInterval(function () {
      showSlide(currentIndex + 1);
    }, autoIntervalMs);
  }

  function stopAutoplay() {
    isPlaying = false;
    if (timer) {
      clearInterval(timer);
      timer = null;
    }
  }

  if (prevBtn) {
    prevBtn.addEventListener('click', function () {
      showSlide(currentIndex - 1);
      startAutoplay();
    });
  }

  if (nextBtn) {
    nextBtn.addEventListener('click', function () {
      showSlide(currentIndex + 1);
      startAutoplay();
    });
  }

  dots.forEach(function (dot, idx) {
    dot.addEventListener('click', function () {
      showSlide(idx);
      startAutoplay();
    });
  });

  // Pause on mouse hover over the slideshow
  wrapper.addEventListener('mouseenter', function () {
    stopAutoplay();
  });

  wrapper.addEventListener('mouseleave', function () {
    startAutoplay();
  });

  // Keyboard navigation when user presses left/right arrow
  document.addEventListener('keydown', function (e) {
    if (document.activeElement && (document.activeElement.tagName === 'INPUT' || document.activeElement.tagName === 'TEXTAREA')) {
      return;
    }
    if (e.key === 'ArrowRight') {
      showSlide(currentIndex + 1);
    } else if (e.key === 'ArrowLeft') {
      showSlide(currentIndex - 1);
    }
  });

  // Start initial slide and autoplay
  showSlide(0);
  startAutoplay();
}

/* -------------------------------------------------------------------
   3. Code Copy Buttons
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
        btn.textContent = '✓ Copied';
        btn.style.backgroundColor = 'var(--accent-gold)';
        btn.style.color = 'var(--text-inverted)';

        setTimeout(function () {
          btn.textContent = originalText;
          btn.style.backgroundColor = '';
          btn.style.color = '';
        }, 2000);
      }).catch(function () {
        btn.textContent = 'Error';
      });
    });
  });
}

/* -------------------------------------------------------------------
   4. User Guide Actions (Download MD, TXT & Print)
   ------------------------------------------------------------------- */
function initGuideActions() {
  var btnDownloadMd = document.getElementById('btn-download-guide-md');
  var btnDownloadTxt = document.getElementById('btn-download-guide-txt');
  var btnPrintGuide = document.getElementById('btn-print-guide');

  // Path to the bundled markdown guide
  var guidePath = 'assets/ErrinILS-User-Guide.md';

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
          triggerBlobDownload(mdText, 'ErrinILS-User-Guide-v1.3.1.md', 'text/markdown;charset=utf-8');
        })
        .catch(function () {
          // Fallback direct link
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
          triggerBlobDownload(text, 'ErrinILS-User-Guide-v1.3.1.txt', 'text/plain;charset=utf-8');
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
   5. Sticky Scroll-Spy for Table of Contents
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
