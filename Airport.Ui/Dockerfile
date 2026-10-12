FROM nginx:1.27-alpine
COPY Airport.Ui/nginx.conf /etc/nginx/conf.d/default.conf
COPY Airport.Ui/ /usr/share/nginx/html/
COPY Data/Airplane_pic.png /usr/share/nginx/html/assets/Airplane_pic.png
COPY Data/clu.jpg /usr/share/nginx/html/assets/clu.jpg
EXPOSE 80
