/* =========================================================
   SEPS APPLICATION JAVASCRIPT
   ========================================================= */

document.addEventListener("DOMContentLoaded", function () {

    const sidebar =
        document.getElementById("appSidebar");

    const sidebarOverlay =
        document.getElementById("sidebarOverlay");

    const openSidebarButton =
        document.getElementById("openSidebar");

    const closeSidebarButton =
        document.getElementById("closeSidebar");

    const desktopSidebarToggle =
        document.getElementById("desktopSidebarToggle");

    const userMenuButton =
        document.getElementById("userMenuButton");

    const userMenuDropdown =
        document.getElementById("userMenuDropdown");


    /* =====================================================
       MOBILE SIDEBAR
       ===================================================== */

    function openMobileSidebar() {

        if (!sidebar) {
            return;
        }

        sidebar.classList.add("mobile-open");

        if (sidebarOverlay) {
            sidebarOverlay.classList.add("active");
        }

        document.body.style.overflow = "hidden";
    }


    function closeMobileSidebar() {

        if (!sidebar) {
            return;
        }

        sidebar.classList.remove("mobile-open");

        if (sidebarOverlay) {
            sidebarOverlay.classList.remove("active");
        }

        document.body.style.overflow = "";
    }


    if (openSidebarButton) {

        openSidebarButton.addEventListener(
            "click",
            openMobileSidebar
        );

    }


    if (closeSidebarButton) {

        closeSidebarButton.addEventListener(
            "click",
            closeMobileSidebar
        );

    }


    if (sidebarOverlay) {

        sidebarOverlay.addEventListener(
            "click",
            closeMobileSidebar
        );

    }


    /* =====================================================
       DESKTOP SIDEBAR
       ===================================================== */

    if (desktopSidebarToggle) {

        desktopSidebarToggle.addEventListener(
            "click",
            function () {

                document.body.classList.toggle(
                    "sidebar-collapsed"
                );

            }
        );

    }


    /* =====================================================
       USER MENU
       ===================================================== */

    if (userMenuButton &&
        userMenuDropdown) {

        userMenuButton.addEventListener(
            "click",
            function (event) {

                event.stopPropagation();

                const isOpen =
                    userMenuDropdown.classList.contains(
                        "open"
                    );

                userMenuDropdown.classList.toggle(
                    "open"
                );

                userMenuButton.setAttribute(
                    "aria-expanded",
                    (!isOpen).toString()
                );

            }
        );


        document.addEventListener(
            "click",
            function (event) {

                if (!userMenuDropdown.contains(event.target) &&
                    !userMenuButton.contains(event.target)) {

                    userMenuDropdown.classList.remove(
                        "open"
                    );

                    userMenuButton.setAttribute(
                        "aria-expanded",
                        "false"
                    );

                }

            }
        );

    }


    /* =====================================================
       ESCAPE KEY
       ===================================================== */

    document.addEventListener(
        "keydown",
        function (event) {

            if (event.key === "Escape") {

                closeMobileSidebar();

                if (userMenuDropdown) {

                    userMenuDropdown.classList.remove(
                        "open"
                    );

                }

                if (userMenuButton) {

                    userMenuButton.setAttribute(
                        "aria-expanded",
                        "false"
                    );

                }

            }

        }
    );


    /* =====================================================
       MOBILE RESIZE
       ===================================================== */

    window.addEventListener(
        "resize",
        function () {

            if (window.innerWidth > 900) {

                closeMobileSidebar();

            }

        }
    );

});