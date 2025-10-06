(() => {
	class MovieReviewSheet {
		constructor(root) {
			this.root = root;
			this.catalog = {};
			try {
				const data = root.dataset.reviewCatalog;
				this.catalog = data ? JSON.parse(data) : {};
			} catch (error) {
				console.error("Không thể đọc cấu hình review", error);
			}

			this.layer = root.querySelector("[data-review-layer]");
			this.confirmDialog = root.querySelector("[data-review-confirm]");
			this.statusDialog = root.querySelector("[data-review-status]");
			this.loader = root.querySelector("[data-review-loader]");
			this.submitButton = root.querySelector("[data-review-submit]");
			this.categorySelect = root.querySelector("[data-review-category]");
			this.tagSelect = root.querySelector("[data-review-tag]");
			this.freeformInput = root.querySelector("[data-review-freeform]");
			this.modeButtons = root.querySelectorAll("[data-review-mode]");
			this.templateSections = root.querySelectorAll("[data-review-template-section]");
			this.freeformSections = root.querySelectorAll("[data-review-freeform-section]");
			this.summaryTemplate = root.querySelector("[data-review-summary-template]");
			this.summaryCategory = root.querySelector("[data-summary-category]");
			this.summaryTag = root.querySelector("[data-summary-tag]");
			this.summaryFreeform = root.querySelector("[data-review-summary-freeform]");
			this.statusIcon = root.querySelector("[data-review-status-icon]");
			this.statusTitle = root.querySelector("[data-review-status-title]");
			this.statusDescription = root.querySelector("[data-review-status-description]");
			this.statusSuccessActions = root.querySelector("[data-review-status-success]");
			this.statusFailureActions = root.querySelector("[data-review-status-failure]");

			this.isSubmitting = false;
			this.mode = "template";
			this.lastSubmitSucceeded = null;

			// Validate required elements exist before continuing. If some are missing,
			// bail out early to avoid runtime errors when methods like resetForm try
			// to access DOM nodes (e.g. setting `.value`).
			const required = [
				"layer",
				"confirmDialog",
				"statusDialog",
				"loader",
				"submitButton",
				"categorySelect",
				"tagSelect",
				"freeformInput",
				"modeButtons",
				"templateSections",
				"freeformSections",
				"summaryTemplate",
				"summaryCategory",
				"summaryTag",
				"summaryFreeform",
				"statusIcon",
				"statusTitle",
				"statusDescription",
				"statusSuccessActions",
				"statusFailureActions",
			];

			const missing = required.filter(key => !this[key]);
			if (missing.length) {
				console.warn("MovieReviewSheet: missing required elements, aborting initialization:", missing);
				this.initialized = false;
				return;
			}

			this.initialized = true;

			this.bindEvents();
			this.resetForm();
		}

		bindEvents() {
			// Ensure required elements exist before binding event listeners to avoid
			// "Cannot read properties of null (reading 'addEventListener')" errors
			const required = [
				"layer",
				"confirmDialog",
				"statusDialog",
				"loader",
				"submitButton",
				"categorySelect",
				"tagSelect",
				"freeformInput",
			];

			const missing = required.filter(key => !this[key]);
			if (missing.length) {
				console.warn("MovieReviewSheet: missing required elements, skipping event binding:", missing);
				return;
			}

			this.layer.querySelectorAll("[data-review-close]").forEach(element => {
				element.addEventListener("click", () => this.close());
			});

			this.modeButtons.forEach(button => {
				button.addEventListener("click", () => this.setMode(button.dataset.reviewMode));
			});

			this.categorySelect.addEventListener("change", () => {
				this.updateTagOptions();
				this.updateSubmitState();
			});

			this.tagSelect.addEventListener("change", () => this.updateSubmitState());
			this.freeformInput.addEventListener("input", () => this.updateSubmitState());

			this.submitButton.addEventListener("click", () => this.requestSubmit());

			this.confirmDialog.querySelector("[data-review-confirm-cancel]").addEventListener("click", () => {
				this.confirmDialog.hidden = true;
			});

			this.confirmDialog.querySelector("[data-review-confirm-send]").addEventListener("click", () => this.confirmSubmit());

			this.statusDialog.querySelector("[data-review-start-another]").addEventListener("click", () => this.startAnotherReview());
			this.statusDialog.querySelector("[data-review-close-complete]").addEventListener("click", () => this.close());
			this.statusDialog.querySelector("[data-review-retry]").addEventListener("click", () => this.retrySubmit());
			this.statusDialog.querySelector("[data-review-dismiss-status]").addEventListener("click", () => this.dismissStatus());

			document.addEventListener("keydown", event => {
				if (event.key === "Escape" && !this.layer.hidden) {
					this.close();
				}
			});
		}

		open() {
			if (this.layer) {
				this.layer.hidden = false;
			}
			document.body.classList.add("review-sheet-open");
		}

		close() {
			this.layer.hidden = true;
			this.confirmDialog.hidden = true;
			this.statusDialog.hidden = true;
			this.setSubmitting(false);
			this.resetForm();
			document.body.classList.remove("review-sheet-open");
		}

		setMode(mode) {
			if (this.mode === mode) {
				return;
			}

			this.mode = mode;
			if (this.modeButtons && this.modeButtons.forEach) {
				this.modeButtons.forEach(button => {
					button.classList.toggle("is-active", button.dataset.reviewMode === mode);
				});
			}

			const showTemplate = mode === "template";
			if (this.templateSections && this.templateSections.forEach) {
				this.templateSections.forEach(section => section.hidden = !showTemplate);
			}
			if (this.freeformSections && this.freeformSections.forEach) {
				this.freeformSections.forEach(section => section.hidden = showTemplate);
			}

			if (showTemplate) {
				if (this.freeformInput) this.freeformInput.value = "";
			} else {
				if (this.categorySelect) this.categorySelect.value = "";
				this.updateTagOptions();
				if (this.tagSelect) {
					this.tagSelect.disabled = true;
					this.tagSelect.selectedIndex = 0;
				}
			}

			this.updateSubmitState();
		}

		updateTagOptions() {
			if (!this.categorySelect || !this.tagSelect) {
				return;
			}

			const selectedCategory = this.categorySelect.value;
			const tags = this.catalog[selectedCategory] || [];
			this.tagSelect.innerHTML = "";

			const placeholder = document.createElement("option");
			placeholder.value = "";
			placeholder.disabled = true;
			placeholder.hidden = true;
			placeholder.selected = true;
			placeholder.textContent = "Chọn nhận xét";
			this.tagSelect.appendChild(placeholder);

			if (!selectedCategory) {
				this.tagSelect.disabled = true;
				return;
			}

			this.tagSelect.disabled = false;
			tags.forEach(tag => {
				const option = document.createElement("option");
				option.value = tag;
				option.textContent = tag;
				this.tagSelect.appendChild(option);
			});
		}

		requestSubmit() {
			if (!this.canSubmit() || this.isSubmitting) {
				return;
			}

			this.renderSummary();
			this.confirmDialog.hidden = false;
		}

		confirmSubmit() {
			this.confirmDialog.hidden = true;
			this.setSubmitting(true);

			// Simulate checkbox removed - use actual API call from review.js
			window.setTimeout(() => {
				const succeeded = true; // Default to success
				this.setSubmitting(false);
				this.showStatus(succeeded);
			}, 1500);
		}

		showStatus(succeeded) {
			this.lastSubmitSucceeded = succeeded;
			this.statusDialog.hidden = false;
			this.statusSuccessActions.hidden = !succeeded;
			this.statusFailureActions.hidden = succeeded;

			const iconBase = succeeded ? "bi-check-circle-fill" : "bi-exclamation-octagon-fill";
			this.statusIcon.className = `review-dialog__status-icon ${iconBase} ${succeeded ? "review-dialog__status-icon--success" : "review-dialog__status-icon--error"}`;
			this.statusTitle.textContent = succeeded ? "Gửi review thành công" : "Không thể gửi review";
			this.statusDescription.textContent = succeeded
				? "Cảm ơn bạn! Review dạng tag đã được hiển thị ngay cho cộng đồng."
				: "Hệ thống đang gặp sự cố giả lập. Bạn có thể thử gửi lại hoặc kiểm tra nội dung trước khi thử lại.";
		}

		startAnotherReview() {
			this.statusDialog.hidden = true;
			this.resetForm();
		}

		retrySubmit() {
			this.statusDialog.hidden = true;
			this.requestSubmit();
		}

		dismissStatus() {
			this.statusDialog.hidden = true;
			this.lastSubmitSucceeded = null;
		}

		setSubmitting(isSubmitting) {
			this.isSubmitting = isSubmitting;
			this.submitButton.disabled = isSubmitting || !this.canSubmit();
			this.loader.hidden = !isSubmitting;
		}

		renderSummary() {
			const templateMode = this.mode === "template";
			if (this.summaryTemplate) this.summaryTemplate.hidden = !templateMode;
			if (this.summaryFreeform) this.summaryFreeform.hidden = templateMode;

			if (templateMode) {
				const category = this.categorySelect ? this.categorySelect.value : "";
				let tag = "";
				if (this.tagSelect) {
					// handle multi-select (if present) by joining selected options
					if (this.tagSelect.multiple) {
						const selected = Array.from(this.tagSelect.selectedOptions).map(opt => opt.value);
						tag = selected.join(", ");
					} else {
						tag = this.tagSelect.value;
					}
				}
				if (this.summaryCategory) this.summaryCategory.textContent = category;
				if (this.summaryTag) this.summaryTag.textContent = tag;
				// Note input has been removed, no need to handle it
			} else {
				const text = this.freeformInput ? this.freeformInput.value.trim() : "";
				const preview = text.length > 140 ? `${text.slice(0, 140)}…` : text;
				if (this.summaryFreeform) this.summaryFreeform.textContent = preview;
			}
		}

		canSubmit() {
			if (this.mode === "template") {
				// If category/tag controls aren't present, cannot submit template mode
				if (!this.categorySelect && !this.tagSelect) return false;

				let categoryOk = true;
				let tagOk = true;

				if (this.categorySelect) categoryOk = Boolean(this.categorySelect.value);
				if (this.tagSelect) {
					if (this.tagSelect.multiple) {
						// ensure at least one selected
						tagOk = Array.from(this.tagSelect.selectedOptions).length > 0;
					} else {
						tagOk = Boolean(this.tagSelect.value);
					}
				}

				return categoryOk && tagOk;
			}

			if (!this.freeformInput) return false;

			return this.freeformInput.value.trim().length > 0;
		}

		updateSubmitState() {
			if (!this.submitButton) return;
			this.submitButton.disabled = !this.canSubmit() || this.isSubmitting;
		}

		resetForm() {
			this.mode = "template";
			if (this.modeButtons && this.modeButtons.forEach) {
				this.modeButtons.forEach(button => button.classList.toggle("is-active", button.dataset.reviewMode === this.mode));
			}
			if (this.templateSections && this.templateSections.forEach) {
				this.templateSections.forEach(section => section.hidden = false);
			}
			if (this.freeformSections && this.freeformSections.forEach) {
				this.freeformSections.forEach(section => section.hidden = true);
			}
			if (this.categorySelect) this.categorySelect.value = "";
			this.updateTagOptions();
			if (this.tagSelect) {
				this.tagSelect.disabled = true;
				this.tagSelect.selectedIndex = 0;
			}
			if (this.freeformInput) this.freeformInput.value = "";
			this.lastSubmitSucceeded = null;
			this.updateSubmitState();
		}
	}

	document.addEventListener("DOMContentLoaded", () => {
		const root = document.querySelector("[data-review-sheet-root]");
		if (!root) {
			return;
		}

		// If the root does not contain legacy category/tag selectors, assume the new
		// review implementation (review.js) is responsible and skip this legacy sheet.
		const legacyCategory = root.querySelector("[data-review-category]");
		const legacyTag = root.querySelector("[data-review-tag]");
		if (!legacyCategory || !legacyTag) {
			return;
		}

		const sheet = new MovieReviewSheet(root);
		if (!sheet.initialized) {
			// Initialization failed (missing DOM nodes). Do not wire triggers.
			return;
		}

		document.querySelectorAll("[data-review-open]").forEach(trigger => {
			trigger.addEventListener("click", event => {
				event.preventDefault();
				sheet.open();
			});
		});
	});
})();
