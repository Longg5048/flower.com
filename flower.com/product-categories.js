(function initializeProductCategories(root, factory) {
  const api = factory();
  if (typeof module !== "undefined" && module.exports) module.exports = api;
  if (root) root.FlowerProductCategories = api;
})(typeof globalThis !== "undefined" ? globalThis : this, function createProductCategories() {
  const categories = {
    birthday: {
      title: "Hoa tặng sinh nhật",
      description: "Những bó hoa rực rỡ dành cho ngày sinh nhật đáng nhớ.",
      keywords: ["birthday", "party", "celebration", "sinh nhat"]
    },
    sympathy: {
      title: "Hoa chia buồn",
      description: "Các mẫu hoa chia buồn sẽ được cập nhật khi có trong cửa hàng.",
      keywords: ["sympathy", "condolence", "funeral", "memorial", "remembrance", "chia buon"]
    },
    occasion: {
      title: "Hoa cho dịp đặc biệt",
      description: "Khám phá hoa dành cho những dịp và khoảnh khắc đáng nhớ.",
      keywords: ["anniversary", "wedding", "romance", "love", "valentine", "occasion", "congratulation", "dip dac biet"]
    },
    flowers: {
      title: "Hoa tươi",
      description: "Chọn một bó hoa tươi để gửi lời yêu thương.",
      keywords: []
    },
    plants: {
      title: "Cây cảnh",
      description: "Các sản phẩm cây cảnh sẽ được cập nhật khi có trong cửa hàng.",
      keywords: ["plant", "succulent", "cactus", "bonsai", "cay canh"]
    },
    gift: {
      title: "Quà tặng",
      description: "Những bó hoa và giỏ quà phù hợp để gửi tặng người thương.",
      keywords: ["gift", "present", "basket", "qua tang"]
    },
    food: {
      title: "Giỏ quà & thực phẩm",
      description: "Các giỏ quà và sản phẩm thực phẩm sẽ xuất hiện tại đây khi có trong cửa hàng.",
      keywords: ["fruit", "chocolate", "food", "snack", "gio qua", "thuc pham"]
    },
    delivery: {
      title: "Giao hoa trong ngày",
      description: "Các mẫu được xác nhận hỗ trợ giao trong ngày sẽ xuất hiện tại đây.",
      sameDayDeliveryOnly: true
    },
    sale: {
      title: "Ưu đãi",
      description: "Sản phẩm khuyến mãi sẽ xuất hiện tại đây khi cửa hàng có chương trình.",
      saleOnly: true
    }
  };

  function normalize(value) {
    return String(value || "")
      .toLocaleLowerCase("vi")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/đ/g, "d");
  }

  function getCategory(key) {
    return categories[key] || null;
  }

  function matchesCategory(product, key) {
    const category = getCategory(key);
    if (!category) return true;
    if (category.sameDayDeliveryOnly) return product.sameDayDelivery === true;
    if (category.saleOnly) {
      return product.onSale === true
        || Number(product.discount) > 0
        || (Number(product.salePrice) > 0 && Number(product.salePrice) < Number(product.price));
    }
    if (key === "flowers") {
      return !/(plant|succulent|cactus|bonsai|cay canh)/.test(normalize(product.category));
    }
    if (!category.keywords.length) return true;

    const details = normalize([
      product.category,
      product.title,
      product.description
    ].join(" "));
    return category.keywords.some((keyword) => details.includes(normalize(keyword)));
  }

  return { getCategory, matchesCategory };
});
