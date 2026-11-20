/**
 * Secure Employee Payroll System (SEPS)
 * Enterprise UI & Responsive Application Script
 */

(function () {
    'use strict';

    // 1. THEME MANAGEMENT (Light / Dark mode)
    const initTheme = () => {
        const savedTheme = localStorage.getItem('seps-theme') ||
            (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

        document.documentElement.setAttribute('data-theme', savedTheme);
        updateThemeToggleIcons(savedTheme);
    };

    const toggleTheme = () => {
        const currentTheme = document.documentElement.getAttribute('data-theme') || 'light';
        const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

        document.documentElement.setAttribute('data-theme', newTheme);
        localStorage.setItem('seps-theme', newTheme);
        updateThemeToggleIcons(newTheme);
        showToast(`Switched to ${newTheme} theme`, 'info');
    };

    const updateThemeToggleIcons = (theme) => {
        const toggleButtons = document.querySelectorAll('.theme-toggle-btn');
        toggleButtons.forEach(btn => {
            const moonIcon = btn.querySelector('.icon-moon');
            const sunIcon = btn.querySelector('.icon-sun');
            if (moonIcon && sunIcon) {
                if (theme === 'dark') {
                    moonIcon.style.display = 'none';
                    sunIcon.style.display = 'inline-block';
                } else {
                    moonIcon.style.display = 'inline-block';
                    sunIcon.style.display = 'none';
                }
            }
            const label = btn.querySelector('.theme-label-text');
            if (label) {
                label.textContent = theme === 'dark' ? 'Dark' : 'Light';
            }
        });
    };

    // 2. SIDEBAR TOGGLE & RESPONSIVE DRAWER
    const initSidebar = () => {
        const desktopToggle = document.getElementById('desktopSidebarToggle');
        const mobileOpen = document.getElementById('openSidebar');
        const mobileClose = document.getElementById('closeSidebar');
        const sidebar = document.getElementById('appSidebar');
        const overlay = document.getElementById('sidebarOverlay');
        const appShell = document.querySelector('.app-shell');

        // Restore desktop collapsed preference
        const isCollapsed = localStorage.getItem('seps-sidebar-collapsed') === 'true';
        if (isCollapsed) {
            document.body.classList.add('sidebar-collapsed');
            if (appShell) appShell.classList.add('sidebar-collapsed');
            if (desktopToggle) desktopToggle.setAttribute('aria-expanded', 'false');
        } else {
            if (desktopToggle) desktopToggle.setAttribute('aria-expanded', 'true');
        }

        if (desktopToggle) {
            desktopToggle.addEventListener('click', (e) => {
                e.preventDefault();
                const willCollapse = !document.body.classList.contains('sidebar-collapsed');
                document.body.classList.toggle('sidebar-collapsed', willCollapse);
                if (appShell) {
                    appShell.classList.toggle('sidebar-collapsed', willCollapse);
                }
                localStorage.setItem('seps-sidebar-collapsed', willCollapse ? 'true' : 'false');
                desktopToggle.setAttribute('aria-expanded', willCollapse ? 'false' : 'true');
            });
        }

        const openMobileMenu = () => {
            if (sidebar) sidebar.classList.add('mobile-open');
            if (overlay) overlay.classList.add('active');
            document.body.style.overflow = 'hidden';
            if (mobileOpen) mobileOpen.setAttribute('aria-expanded', 'true');
        };

        const closeMobileMenu = () => {
            if (sidebar) sidebar.classList.remove('mobile-open');
            if (overlay) overlay.classList.remove('active');
            document.body.style.overflow = '';
            if (mobileOpen) mobileOpen.setAttribute('aria-expanded', 'false');
        };

        if (mobileOpen) {
            mobileOpen.setAttribute('aria-expanded', 'false');
            mobileOpen.addEventListener('click', (e) => {
                e.preventDefault();
                openMobileMenu();
            });
        }

        if (mobileClose) {
            mobileClose.addEventListener('click', (e) => {
                e.preventDefault();
                closeMobileMenu();
            });
        }

        if (overlay) {
            overlay.addEventListener('click', closeMobileMenu);
        }

        // Close mobile drawer on Escape key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'Escape' && sidebar && sidebar.classList.contains('mobile-open')) {
                closeMobileMenu();
            }
        });

        // Close mobile drawer on selecting any navigation link
        if (sidebar) {
            const navLinks = sidebar.querySelectorAll('.nav-item');
            navLinks.forEach(link => {
                link.addEventListener('click', () => {
                    if (window.innerWidth <= 900) {
                        closeMobileMenu();
                    }
                });
            });
        }

        // Highlight Active Sidebar Item
        const currentPath = window.location.pathname.toLowerCase();
        const allNavLinks = document.querySelectorAll('.sidebar-navigation .nav-item');
        allNavLinks.forEach(link => {
            const href = link.getAttribute('href')?.toLowerCase();
            if (href && href !== '#' && href !== '/' && currentPath.startsWith(href)) {
                link.classList.add('active');
            } else if (href === '/' && (currentPath === '/' || currentPath === '/home' || currentPath === '/home/index')) {
                link.classList.add('active');
            }
        });
    };

    // 3. USER MENU DROPDOWN
    const initUserMenu = () => {
        const userMenuBtn = document.getElementById('userMenuButton');
        const userDropdown = document.getElementById('userMenuDropdown');

        if (userMenuBtn && userDropdown) {
            userMenuBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                const isExpanded = userMenuBtn.getAttribute('aria-expanded') === 'true';
                userMenuBtn.setAttribute('aria-expanded', !isExpanded);
                userDropdown.classList.toggle('open');
            });

            document.addEventListener('click', (e) => {
                if (!userMenuBtn.contains(e.target) && !userDropdown.contains(e.target)) {
                    userMenuBtn.setAttribute('aria-expanded', 'false');
                    userDropdown.classList.remove('open');
                }
            });
        }
    };

    // 4. GLOBAL SEARCH NAVIGATION
    const initGlobalSearch = () => {
        const searchInput = document.querySelector('.global-search input[type="search"]');
        if (searchInput) {
            searchInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    const query = searchInput.value.trim();
                    if (query) {
                        window.location.href = `/Employees?search=${encodeURIComponent(query)}`;
                    }
                }
            });
        }
    };

    // 5. DATA CONFIRMATION DIALOGS
    const initConfirmations = () => {
        document.addEventListener('submit', (e) => {
            const target = e.target;
            const confirmMsg = target.getAttribute('data-confirm') || target.querySelector('[data-confirm]')?.getAttribute('data-confirm');
            if (confirmMsg) {
                if (!confirm(confirmMsg)) {
                    e.preventDefault();
                }
            }
        });
    };

    // 6. TOAST NOTIFICATION ENGINE
    window.showToast = (message, type = 'info') => {
        const container = document.getElementById('toastContainer');
        if (!container) return;

        const toast = document.createElement('div');
        toast.className = `toast toast-${type}`;
        toast.style.cssText = `
            background: var(--seps-surface, #ffffff);
            color: var(--seps-text, #202124);
            border-left: 4px solid ${type === 'success' ? '#188038' : type === 'danger' ? '#d93025' : '#1a73e8'};
            box-shadow: 0 4px 12px rgba(0,0,0,0.15);
            padding: 12px 18px;
            margin-top: 10px;
            border-radius: 6px;
            display: flex;
            align-items: center;
            justify-content: space-between;
            min-width: 280px;
            max-width: 420px;
            font-size: 0.9rem;
            animation: slideIn 0.25s ease-out;
            z-index: 9999;
        `;

        toast.innerHTML = `
            <span>${message}</span>
            <button type="button" style="background: none; border: none; font-size: 1.2rem; cursor: pointer; color: var(--seps-text-secondary); margin-left: 12px;">&times;</button>
        `;

        const closeBtn = toast.querySelector('button');
        closeBtn.addEventListener('click', () => toast.remove());

        container.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity 0.3s ease';
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    };

    // DOM CONTENT LOADED INITIALIZER
    document.addEventListener('DOMContentLoaded', () => {
        initTheme();
        initSidebar();
        initUserMenu();
        initGlobalSearch();
        initConfirmations();

        // Wire Theme toggle buttons
        document.querySelectorAll('.theme-toggle-btn').forEach(btn => {
            btn.addEventListener('click', toggleTheme);
        });
    });
})();
