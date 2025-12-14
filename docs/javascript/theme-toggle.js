(function(){
  const toggleId = 'theme-toggle';
  const storageKey = 'p7_theme';

  function applyTheme(theme) {
    if (theme === 'light') {
      document.documentElement.classList.add('theme-light');
      const btn = document.getElementById(toggleId);
      if (btn) { btn.textContent = 'Light'; btn.setAttribute('aria-pressed','true'); }
    } else {
      document.documentElement.classList.remove('theme-light');
      const btn = document.getElementById(toggleId);
      if (btn) { btn.textContent = 'Dark'; btn.setAttribute('aria-pressed','false'); }
    }
  }

  function init() {
    const saved = localStorage.getItem(storageKey);
    const prefersLight = window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches;
    const initial = saved || (prefersLight ? 'light' : 'dark');
    applyTheme(initial);

    const btn = document.getElementById(toggleId);
    if (!btn) return;

    btn.addEventListener('click', () => {
      const isLight = document.documentElement.classList.contains('theme-light');
      const next = isLight ? 'dark' : 'light';
      applyTheme(next);
      try { localStorage.setItem(storageKey, next); } catch(e){}
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else init();
})();
