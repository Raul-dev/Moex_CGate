# From base OS image
#cd ../filebeat_base
#docker build -t filebeat-alpine . --progress plain
#create yourrepo/filebeat-alpine:8.0.0

FROM raulamailru/filebeat-alpine:8.0.0

COPY filebeat.yml /usr/share/filebeat/filebeat.yml

COPY filebeat.yml /
RUN cd /filebeat && \
    cp filebeat /usr/bin 

RUN  rm -rf /filebeat/filebeat.yml && \
     cp /filebeat.yml /filebeat/ 

RUN ls -ltr /filebeat && \
    cat /filebeat/filebeat.yml

VOLUME /filebeat/data

#RUN chown -R root /usr/share/filebeat/
#RUN chmod -R go-w /usr/share/filebeat/
RUN mkdir -p /src/logs/
WORKDIR /filebeat/
CMD ["./filebeat","-e","-c", "filebeat.yml"]