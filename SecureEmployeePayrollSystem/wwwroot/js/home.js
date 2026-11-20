/* ==========================================================================
   SEPS PUBLIC HOMEPAGE JAVASCRIPT (home.js)
   Interactivity for Navigation, Mobile Menu, Accordions & Forms
   ========================================================================== */

document.addEventListener('DOMContentLoaded', function () {
    // 1. MOBILE MENU TOGGLE
    const mobileToggle = document.getElementById('shMobileToggle');
    const mobileMenu = document.getElementById('shMobileMenu');

    if (mobileToggle && mobileMenu) {
        mobileToggle.addEventListener('click', function () {
            const isOpen = mobileMenu.classList.toggle('open');
            mobileToggle.setAttribute('aria-expanded', isOpen);
        });

        // Close mobile menu when a navigation link is clicked
        const mobileLinks = mobileMenu.querySelectorAll('a');
        mobileLinks.forEach(function (link) {
            link.addEventListener('click', function () {
                mobileMenu.classList.remove('open');
                mobileToggle.setAttribute('aria-expanded', 'false');
            });
        });
    }

    // 2. SMOOTH SCROLLING FOR IN-PAGE ANCHORS
    const anchorLinks = document.querySelectorAll('.seps-home a[href^="#"]');
    anchorLinks.forEach(function (anchor) {
        anchor.addEventListener('click', function (e) {
            const targetId = this.getAttribute('href');
            if (targetId && targetId !== '#') {
                const targetElement = document.querySelector(targetId);
                if (targetElement) {
                    e.preventDefault();
                    const headerOffset = 84;
                    const elementPosition = targetElement.getBoundingClientRect().top;
                    const offsetPosition = elementPosition + window.pageYOffset - headerOffset;

                    window.scrollTo({
                        top: offsetPosition,
                        behavior: 'smooth'
                    });
                }
            }
        });
    });

    // 3. FAQ ACCORDION INTERACTIVITY
    const accordionButtons = document.querySelectorAll('.sh-accordion-btn');
    accordionButtons.forEach(function (btn) {
        btn.addEventListener('click', function () {
            const item = this.closest('.sh-accordion-item');
            const isActive = item.classList.contains('active');

            // Optional: Close other open accordion items
            document.querySelectorAll('.sh-accordion-item').forEach(function (otherItem) {
                if (otherItem !== item) {
                    otherItem.classList.remove('active');
                    const otherBtn = otherItem.querySelector('.sh-accordion-btn');
                    if (otherBtn) otherBtn.setAttribute('aria-expanded', 'false');
                }
            });

            // Toggle current item
            item.classList.toggle('active', !isActive);
            this.setAttribute('aria-expanded', !isActive);
        });
    });

    // 4. FEEDBACK RATING STAR SELECTOR
    const ratingStars = document.querySelectorAll('.sh-rating-option');
    const ratingInput = document.getElementById('feedbackRatingInput');

    if (ratingStars.length && ratingInput) {
        ratingStars.forEach(function (star) {
            star.addEventListener('click', function () {
                const val = parseInt(this.getAttribute('data-value'), 10);
                ratingInput.value = val;
                updateStars(val);
            });

            star.addEventListener('mouseenter', function () {
                const val = parseInt(this.getAttribute('data-value'), 10);
                highlightStars(val);
            });
        });

        const ratingContainer = document.querySelector('.sh-rating-select');
        if (ratingContainer) {
            ratingContainer.addEventListener('mouseleave', function () {
                const currentVal = parseInt(ratingInput.value || 5, 10);
                updateStars(currentVal);
            });
        }

        function highlightStars(count) {
            ratingStars.forEach(function (star) {
                const starVal = parseInt(star.getAttribute('data-value'), 10);
                star.classList.toggle('hovered', starVal <= count);
            });
        }

        function updateStars(count) {
            ratingStars.forEach(function (star) {
                const starVal = parseInt(star.getAttribute('data-value'), 10);
                star.classList.toggle('selected', starVal <= count);
                star.classList.remove('hovered');
            });
        }

        // Initialize with default value
        updateStars(parseInt(ratingInput.value || 5, 10));
    }

    // 5. CONTACT FORM AJAX SUBMISSION
    const contactForm = document.getElementById('shContactForm');
    const contactAlert = document.getElementById('shContactAlert');

    if (contactForm && contactAlert) {
        contactForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const submitBtn = contactForm.querySelector('button[type="submit"]');
            const originalBtnText = submitBtn ? submitBtn.innerText : 'Send Message';

            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerText = 'Sending...';
            }

            const formData = new FormData(contactForm);

            fetch(contactForm.getAttribute('action'), {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            })
            .then(function (response) {
                return response.json();
            })
            .then(function (data) {
                contactAlert.style.display = 'block';
                if (data.success) {
                    contactAlert.className = 'sh-alert sh-alert-success';
                    contactAlert.innerHTML = '✓ ' + (data.message || 'Your inquiry has been submitted successfully.');
                    contactForm.reset();
                } else {
                    contactAlert.className = 'sh-alert sh-alert-danger';
                    contactAlert.innerHTML = '⚠ ' + (data.message || 'Unable to submit your message. Please check the fields and try again.');
                }
            })
            .catch(function () {
                contactAlert.style.display = 'block';
                contactAlert.className = 'sh-alert sh-alert-danger';
                contactAlert.innerHTML = '⚠ An unexpected network error occurred. Please try again later.';
            })
            .finally(function () {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerText = originalBtnText;
                }
            });
        });
    }

    // 6. FEEDBACK FORM AJAX SUBMISSION
    const feedbackForm = document.getElementById('shFeedbackForm');
    const feedbackAlert = document.getElementById('shFeedbackAlert');

    if (feedbackForm && feedbackAlert) {
        feedbackForm.addEventListener('submit', function (e) {
            e.preventDefault();
            const submitBtn = feedbackForm.querySelector('button[type="submit"]');
            const originalBtnText = submitBtn ? submitBtn.innerText : 'Submit Feedback';

            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.innerText = 'Submitting...';
            }

            const formData = new FormData(feedbackForm);

            fetch(feedbackForm.getAttribute('action'), {
                method: 'POST',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                body: formData
            })
            .then(function (response) {
                return response.json();
            })
            .then(function (data) {
                feedbackAlert.style.display = 'block';
                if (data.success) {
                    feedbackAlert.className = 'sh-alert sh-alert-success';
                    feedbackAlert.innerHTML = '✓ ' + (data.message || 'Thank you for your valuable feedback!');
                    feedbackForm.reset();
                    if (ratingInput) ratingInput.value = 5;
                    const ratingStars = document.querySelectorAll('.sh-rating-option');
                    ratingStars.forEach(s => s.classList.add('selected'));
                } else {
                    feedbackAlert.className = 'sh-alert sh-alert-danger';
                    feedbackAlert.innerHTML = '⚠ ' + (data.message || 'Unable to submit feedback. Please check the fields and try again.');
                }
            })
            .catch(function () {
                feedbackAlert.style.display = 'block';
                feedbackAlert.className = 'sh-alert sh-alert-danger';
                feedbackAlert.innerHTML = '⚠ An unexpected error occurred. Please try again.';
            })
            .finally(function () {
                if (submitBtn) {
                    submitBtn.disabled = false;
                    submitBtn.innerText = originalBtnText;
                }
            });
        });
    }

    // =========================================================================
    // 7. SCROLL-TRIGGERED REVEAL ANIMATIONS (IntersectionObserver)
    // =========================================================================
    const revealElements = document.querySelectorAll(
        '.reveal-on-scroll, .reveal-fade-up, .reveal-slide-left, .reveal-slide-right, .reveal-scale'
    );

    if (revealElements.length > 0) {
        if ('IntersectionObserver' in window) {
            const revealObserver = new IntersectionObserver(function (entries, observer) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        entry.target.classList.add('is-revealed');
                        observer.unobserve(entry.target);
                    }
                });
            }, {
                root: null,
                rootMargin: '0px 0px -40px 0px',
                threshold: 0.12
            });

            revealElements.forEach(function (el) {
                revealObserver.observe(el);
            });
        } else {
            // Fallback for older browsers
            revealElements.forEach(function (el) {
                el.classList.add('is-revealed');
            });
        }
    }

    // =========================================================================
    // 8. STICKY HEADER SCROLL SHADOW & ACTIVE NAV HIGHLIGHT
    // =========================================================================
    const header = document.querySelector('.sh-header');
    const navLinks = document.querySelectorAll('.sh-nav .sh-nav-link');
    const trackedSections = document.querySelectorAll('section[id]');

    function handleScrollState() {
        if (!header) return;
        if (window.scrollY > 20) {
            header.classList.add('scrolled');
        } else {
            header.classList.remove('scrolled');
        }

        // Active navigation link tracking
        if (navLinks.length > 0 && trackedSections.length > 0) {
            let currentActiveId = '';
            const scrollPosition = window.pageYOffset + 120;

            trackedSections.forEach(function (section) {
                const sectionTop = section.offsetTop;
                const sectionHeight = section.offsetHeight;
                if (scrollPosition >= sectionTop && scrollPosition < sectionTop + sectionHeight) {
                    currentActiveId = section.getAttribute('id');
                }
            });

            navLinks.forEach(function (link) {
                const href = link.getAttribute('href');
                if (href === '#' + currentActiveId) {
                    link.classList.add('active');
                } else {
                    link.classList.remove('active');
                }
            });
        }
    }

    window.addEventListener('scroll', handleScrollState, { passive: true });
    handleScrollState();

    // =========================================================================
    // 9. PROFESSIONAL FEEDBACK CAROUSEL (10 Entries Showcase)
    // =========================================================================
    const carouselWrapper = document.getElementById('shFeedbackCarouselWrapper');
    const carouselTrack = document.getElementById('shCarouselTrack');
    const prevBtn = document.getElementById('shCarouselPrev');
    const nextBtn = document.getElementById('shCarouselNext');
    const dotsContainer = document.getElementById('shCarouselDots');

    if (carouselTrack && carouselWrapper) {
        const slides = carouselTrack.querySelectorAll('.sh-carousel-slide');
        const totalSlides = slides.length;
        let currentIndex = 0;
        let autoPlayTimer = null;
        let isTouching = false;
        let touchStartX = 0;
        let touchCurrentX = 0;

        function getCardsPerView() {
            const w = window.innerWidth;
            if (w >= 1024) return 3;
            if (w >= 640) return 2;
            return 1;
        }

        function getMaxIndex() {
            const perView = getCardsPerView();
            return Math.max(0, totalSlides - perView);
        }

        function buildDots() {
            if (!dotsContainer) return;
            dotsContainer.innerHTML = '';
            const maxIdx = getMaxIndex();
            const totalDots = maxIdx + 1;

            for (let i = 0; i < totalDots; i++) {
                const dot = document.createElement('button');
                dot.type = 'button';
                dot.className = 'sh-carousel-dot' + (i === currentIndex ? ' active' : '');
                dot.setAttribute('aria-label', 'Go to slide set ' + (i + 1));
                dot.addEventListener('click', function () {
                    goToSlide(i);
                });
                dotsContainer.appendChild(dot);
            }
        }

        function updateCarousel(animate) {
            const perView = getCardsPerView();
            const maxIdx = getMaxIndex();

            if (currentIndex > maxIdx) {
                currentIndex = maxIdx;
            }
            if (currentIndex < 0) {
                currentIndex = 0;
            }

            if (animate === false) {
                carouselTrack.style.transition = 'none';
            } else {
                carouselTrack.style.transition = 'transform 0.5s cubic-bezier(0.25, 1, 0.5, 1)';
            }

            const stepPercent = 100 / perView;
            const translateX = -(currentIndex * stepPercent);
            carouselTrack.style.transform = 'translate3d(' + translateX + '%, 0, 0)';

            // Update button states
            if (prevBtn) {
                prevBtn.disabled = (currentIndex === 0);
            }
            if (nextBtn) {
                nextBtn.disabled = (currentIndex >= maxIdx);
            }

            // Update active dot
            if (dotsContainer) {
                const dots = dotsContainer.querySelectorAll('.sh-carousel-dot');
                dots.forEach(function (dot, idx) {
                    dot.classList.toggle('active', idx === currentIndex);
                });
            }
        }

        function goToSlide(index) {
            currentIndex = index;
            updateCarousel(true);
        }

        function nextSlide() {
            const maxIdx = getMaxIndex();
            if (currentIndex >= maxIdx) {
                currentIndex = 0; // Wrap around smoothly
            } else {
                currentIndex++;
            }
            updateCarousel(true);
        }

        function prevSlide() {
            const maxIdx = getMaxIndex();
            if (currentIndex <= 0) {
                currentIndex = maxIdx; // Wrap to end
            } else {
                currentIndex--;
            }
            updateCarousel(true);
        }

        if (nextBtn) {
            nextBtn.addEventListener('click', function () {
                nextSlide();
                restartAutoPlay();
            });
        }

        if (prevBtn) {
            prevBtn.addEventListener('click', function () {
                prevSlide();
                restartAutoPlay();
            });
        }

        // Auto-play mechanism (5 second interval)
        function startAutoPlay() {
            stopAutoPlay();
            autoPlayTimer = setInterval(function () {
                nextSlide();
            }, 5000);
        }

        function stopAutoPlay() {
            if (autoPlayTimer) {
                clearInterval(autoPlayTimer);
                autoPlayTimer = null;
            }
        }

        function restartAutoPlay() {
            stopAutoPlay();
            startAutoPlay();
        }

        // Pause on mouse enter, resume on mouse leave
        carouselWrapper.addEventListener('mouseenter', stopAutoPlay);
        carouselWrapper.addEventListener('mouseleave', startAutoPlay);
        carouselWrapper.addEventListener('focusin', stopAutoPlay);
        carouselWrapper.addEventListener('focusout', startAutoPlay);

        // Touch swipe support for mobile
        carouselTrack.addEventListener('touchstart', function (e) {
            isTouching = true;
            touchStartX = e.touches[0].clientX;
            touchCurrentX = touchStartX;
            stopAutoPlay();
        }, { passive: true });

        carouselTrack.addEventListener('touchmove', function (e) {
            if (!isTouching) return;
            touchCurrentX = e.touches[0].clientX;
        }, { passive: true });

        carouselTrack.addEventListener('touchend', function () {
            if (!isTouching) return;
            isTouching = false;
            const diffX = touchStartX - touchCurrentX;
            const threshold = 45; // Min swipe distance in px

            if (diffX > threshold) {
                nextSlide();
            } else if (diffX < -threshold) {
                prevSlide();
            }
            startAutoPlay();
        });

        // Window resize debounced recalculation
        let resizeTimer = null;
        window.addEventListener('resize', function () {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(function () {
                buildDots();
                updateCarousel(false);
            }, 100);
        });

        // Initialize carousel
        buildDots();
        updateCarousel(false);
        startAutoPlay();
    }
});
