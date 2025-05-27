// Theme toggle functionality - standalone script for all pages
function applyTheme(theme) {
    const bodyElement = document.body;
    console.log('Applying theme:', theme);
    if (theme === 'light') { 
        bodyElement.classList.add('light-mode'); 
    } else { 
        bodyElement.classList.remove('light-mode'); 
    }
    console.log('Body classes after theme change:', bodyElement.className);
}

// Initialize theme on page load
function initializeTheme() {
    const savedTheme = localStorage.getItem('theme');
    console.log('Saved theme from localStorage:', savedTheme);
    
    if (savedTheme) { 
        applyTheme(savedTheme); 
    } else {
        // Check system preference
        if (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches) { 
            console.log('System prefers light mode');
            applyTheme('light'); 
        } else { 
            console.log('System prefers dark mode or no preference');
            applyTheme('dark'); 
        }
    }
}

// Set up theme toggle button
function setupThemeToggle() {
    const themeToggleButton = document.getElementById('themeToggle');
    console.log('Theme toggle button found:', !!themeToggleButton);
    
    if (themeToggleButton) {
        // Remove any existing listeners to prevent duplicates
        themeToggleButton.removeEventListener('click', handleThemeToggle);
        themeToggleButton.addEventListener('click', handleThemeToggle);
        console.log('Theme toggle event listener attached');
    } else {
        console.error('Theme toggle button not found! Make sure element with id="themeToggle" exists');
    }
}

// Separate function for the click handler to avoid duplicates
function handleThemeToggle() {
    console.log('Theme toggle clicked!');
    const bodyElement = document.body;
    const currentTheme = bodyElement.classList.contains('light-mode') ? 'light' : 'dark';
    const newTheme = currentTheme === 'light' ? 'dark' : 'light';
    console.log('Switching from', currentTheme, 'to', newTheme);
    
    applyTheme(newTheme);
    localStorage.setItem('theme', newTheme);
    console.log('Theme saved to localStorage:', newTheme);
}

// Initialize everything
function init() {
    console.log('Theme script initializing...');
    initializeTheme();
    setupThemeToggle();
}

// Multiple ways to ensure initialization happens
if (document.readyState === 'loading') {
    console.log('DOM still loading, waiting for DOMContentLoaded');
    document.addEventListener('DOMContentLoaded', init);
} else {
    console.log('DOM already loaded, initializing immediately');
    init();
}

// Also try after a short delay as a fallback
setTimeout(() => {
    if (!document.getElementById('themeToggle')?.onclick && !document.getElementById('themeToggle')?._hasThemeListener) {
        console.log('Fallback initialization after 100ms');
        setupThemeToggle();
        // Mark that we've set up the listener
        const button = document.getElementById('themeToggle');
        if (button) button._hasThemeListener = true;
    }
}, 100); 