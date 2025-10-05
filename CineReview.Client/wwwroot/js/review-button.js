(() => {
    "use strict";

    /**
     * Smart Write Review Button
     * Check if user has already reviewed the movie
     */

    document.addEventListener("DOMContentLoaded", () => {
        const movieIdElement = document.querySelector("[data-movie-id]");
        const movieId = movieIdElement ? parseInt(movieIdElement.dataset.movieId) : null;

        if (!movieId) return;

        const root = document.querySelector("[data-review-sheet-root]");
        if (!root) return;

        const apiBaseUrl = (root.dataset.apiBaseUrl || "").trim().replace(/\/+$/, "");
        if (!apiBaseUrl) return;

        const writeReviewButtons = document.querySelectorAll("[data-write-review]");
        const yourReviewDialog = root.querySelector("[data-your-review-dialog]");
        const yourReviewCloseButtons = root.querySelectorAll("[data-your-review-close]");

        // Dialog elements
        const yourReviewType = root.querySelector("[data-your-review-type]");
        const yourReviewStatus = root.querySelector("[data-your-review-status]");
        const yourReviewContent = root.querySelector("[data-your-review-content]");
        const yourReviewRating = root.querySelector("[data-your-review-rating]");
        const yourReviewFair = root.querySelector("[data-your-review-fair]");
        const yourReviewUnfair = root.querySelector("[data-your-review-unfair]");
        const yourReviewScore = root.querySelector("[data-your-review-score]");

        let userReview = null;

        // Get auth token
        const getAuthToken = () => {
            if (window.CineReviewAuth && typeof window.CineReviewAuth.getToken === "function") {
                return window.CineReviewAuth.getToken();
            }
            return null;
        };

        // Get user profile
        const getUserProfile = () => {
            if (window.CineReviewAuth && typeof window.CineReviewAuth.getProfile === "function") {
                return window.CineReviewAuth.getProfile();
            }
            return null;
        };

        // Show skeleton loading on buttons
        const showButtonLoading = () => {
            writeReviewButtons.forEach(btn => {
                btn.disabled = true;
                btn.innerHTML = `
                    <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                    <span>Đang tải...</span>
                `;
            });
        };

        // Update button state
        const updateButtonState = (hasReview) => {
            writeReviewButtons.forEach(btn => {
                btn.disabled = false;
                if (hasReview) {
                    btn.innerHTML = `
                        <i class="bi bi-check-circle me-2"></i>
                        <span>Đã review</span>
                    `;
                    btn.classList.add("btn-success");
                    btn.classList.remove("btn-ghost", "btn-primary");
                } else {
                    btn.innerHTML = `Viết review`;
                    btn.classList.remove("btn-success");
                    // Keep original classes
                }
            });
        };

        // Show your review dialog
        const showYourReviewDialog = () => {
            if (!userReview || !yourReviewDialog) return;

            // Type
            if (yourReviewType) {
                yourReviewType.textContent = userReview.type === 0 ? "Tag Review" : "Review chi tiết";
            }

            // Status
            if (yourReviewStatus) {
                const statusMap = {
                    0: "Chờ duyệt",
                    1: "Đã duyệt",
                    2: "Đã xóa"
                };
                yourReviewStatus.textContent = statusMap[userReview.status] || "Không xác định";
                yourReviewStatus.className = `badge ms-2 ${userReview.status === 1 ? "bg-success" : userReview.status === 0 ? "bg-warning" : "bg-danger"}`;
            }

            // Content
            if (yourReviewContent) {
                let content = "";
                if (userReview.type === 0 && userReview.descriptionTag && userReview.descriptionTag.length > 0) {
                    content = `[${userReview.descriptionTag.join(" / ")}]`;
                    if (userReview.description) {
                        content += ` ${userReview.description}`;
                    }
                } else {
                    content = userReview.description || "Không có nội dung";
                }
                yourReviewContent.textContent = content;
            }

            // Rating
            if (yourReviewRating) {
                const stars = '★'.repeat(Math.min(userReview.rating, 10)) + '☆'.repeat(Math.max(0, 10 - userReview.rating));
                yourReviewRating.textContent = stars;
            }

            // Votes
            if (yourReviewFair) yourReviewFair.textContent = userReview.fairVotes || 0;
            if (yourReviewUnfair) yourReviewUnfair.textContent = userReview.unfairVotes || 0;

            // Score
            if (yourReviewScore) {
                yourReviewScore.textContent = (userReview.communicationScore || 0).toFixed(1);
            }

            // Show dialog
            yourReviewDialog.removeAttribute("hidden");
        };

        // Hide your review dialog
        const hideYourReviewDialog = () => {
            if (yourReviewDialog) {
                yourReviewDialog.setAttribute("hidden", "");
            }
        };

        // Check if user has reviewed
        const checkUserReview = async () => {
            const token = getAuthToken();
            if (!token) {
                // Not logged in, show normal button
                updateButtonState(false);
                return;
            }

            const profile = getUserProfile();
            if (!profile || !profile.userId) {
                updateButtonState(false);
                return;
            }

            showButtonLoading();

            try {
                const response = await fetch(`${apiBaseUrl}/api/Review/user/${profile.userId}/movie/${movieId}`, {
                    method: "GET",
                    headers: {
                        "Accept": "application/json",
                        "Authorization": `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error("Không thể kiểm tra review");
                }

                const result = await response.json();

                if (result.isSuccess && result.data) {
                    // User has reviewed
                    userReview = result.data;
                    updateButtonState(true);
                } else {
                    // User hasn't reviewed
                    userReview = null;
                    updateButtonState(false);
                }

            } catch (error) {
                console.error("Lỗi khi kiểm tra review:", error);
                // On error, assume no review
                userReview = null;
                updateButtonState(false);
            }
        };

        // Handle button click
        writeReviewButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                e.stopPropagation();

                if (userReview) {
                    // Show existing review
                    showYourReviewDialog();
                } else {
                    // Open review sheet (handled by review.js)
                    if (window.CineReviewSheet && typeof window.CineReviewSheet.open === "function") {
                        window.CineReviewSheet.open();
                    }
                }
            });
        });

        // Close dialog buttons
        yourReviewCloseButtons.forEach(button => {
            button.addEventListener("click", e => {
                e.preventDefault();
                hideYourReviewDialog();
            });
        });

        // Check on page load
        checkUserReview();

        // Re-check after successful review submission
        if (window.CineReviewSheet) {
            const originalReload = window.CineReviewSheet.reload;
            window.CineReviewSheet.reload = function() {
                if (originalReload) originalReload();
                checkUserReview();
            };
        }

        // Expose API
        window.CineReviewButton = {
            checkUserReview,
            showYourReview: showYourReviewDialog,
            hideYourReview: hideYourReviewDialog
        };
    });
})();
